using Cysharp.Threading.Tasks;
using DCL.Tasks;
using DCLServices.Lambdas;
using MainScripts.DCL.Helpers.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DCLServices.WearablesCatalogService
{
    /// <summary>
    /// This service implements a direct way of getting wearables sending the requests directly to lambdas.
    /// </summary>
    public class LambdasWearablesCatalogService : IWearablesCatalogService, ILambdaServiceConsumer<WearableWithDefinitionResponse>
    {
        public BaseDictionary<string, WearableItem> WearablesCatalog { get; }

        private const string ASSET_BUNDLES_URL_ORG = "https://content-assets-as-bundle.decentraland.org/";
        private const string TEXTURES_URL_ORG = "https://interconnected.online/content/contents/";
        private const string PAGINATED_WEARABLES_END_POINT = "nfts/wearables/";
        private const string NON_PAGINATED_WEARABLES_END_POINT = "collections/wearables/";
        private const string BASE_WEARABLES_COLLECTION_ID = "urn:decentraland:off-chain:base-avatars";
        private const int REQUESTS_TIME_OUT_SECONDS = 45;
        private const int MAX_WEARABLES_PER_REQUEST = 200;

        private readonly ILambdasService lambdasService;
        private readonly Dictionary<string, int> wearablesInUseCounters = new ();
        private readonly Dictionary<string, LambdaResponsePagePointer<WearableWithDefinitionResponse>> ownerWearablesPagePointers = new ();
        private readonly Dictionary<(string, string), LambdaResponsePagePointer<WearableWithDefinitionResponse>> thirdPartyCollectionPagePointers = new ();
        private readonly List<string> pendingWearablesToRequest = new ();
        private CancellationTokenSource serviceCts;
        private UniTaskCompletionSource<IReadOnlyList<WearableItem>> lastRequestSource;

        public LambdasWearablesCatalogService(BaseDictionary<string, WearableItem> wearablesCatalog,
            ILambdasService lambdasService)
        {
            this.lambdasService = lambdasService;
            WearablesCatalog = wearablesCatalog;
        }

        public void Initialize()
        {
            serviceCts = serviceCts.SafeRestart();
        }

        public void Dispose()
        {
            serviceCts.SafeCancelAndDispose();
            serviceCts = null;
            Clear();
        }

        public async UniTask<IReadOnlyList<WearableItem>> RequestOwnedWearablesAsync(string userId, int pageNumber, int pageSize, bool cleanCachedPages, CancellationToken ct)
        {
            var createNewPointer = false;
            if (!ownerWearablesPagePointers.TryGetValue(userId, out var pagePointer))
            {
                createNewPointer = true;
            }
            else if (cleanCachedPages)
            {
                pagePointer.Dispose();
                ownerWearablesPagePointers.Remove(userId);
                createNewPointer = true;
            }

            if (createNewPointer)
            {
                ownerWearablesPagePointers[userId] = pagePointer = new LambdaResponsePagePointer<WearableWithDefinitionResponse>(
                    PAGINATED_WEARABLES_END_POINT + userId,
                    pageSize, ct, this);
            }

            var pageResponse = await pagePointer.GetPageAsync(pageNumber, ct);

            if (!pageResponse.success)
                throw new Exception($"The request of the owned wearables for '{userId}' failed!");

            var wearables = pageResponse.response.wearables.Select(x => x.definition).ToList();
            MapLambdasDataIntoWearableItem(wearables);
            AddWearablesToCatalog(wearables);

            return wearables;
        }

        public async UniTask<IReadOnlyList<WearableItem>> RequestBaseWearablesAsync(CancellationToken ct)
        {
            var serviceResponse = await lambdasService.Get<WearableWithoutDefinitionResponse>(
                NON_PAGINATED_WEARABLES_END_POINT,
                NON_PAGINATED_WEARABLES_END_POINT,
                REQUESTS_TIME_OUT_SECONDS,
                urlEncodedParams: ("collectionId", BASE_WEARABLES_COLLECTION_ID),
                cancellationToken: ct);

            if (!serviceResponse.success)
                throw new Exception("The request of the base wearables failed!");

            MapLambdasDataIntoWearableItem(serviceResponse.response.wearables);
            AddWearablesToCatalog(serviceResponse.response.wearables);

            return serviceResponse.response.wearables;
        }

        public async UniTask<IReadOnlyList<WearableItem>> RequestThirdPartyWearablesByCollectionAsync(string userId, string collectionId, int pageNumber, int pageSize, bool cleanCachedPages, CancellationToken ct)
        {
            var createNewPointer = false;
            if (!thirdPartyCollectionPagePointers.TryGetValue((userId, collectionId), out var pagePointer))
            {
                createNewPointer = true;
            }
            else if (cleanCachedPages)
            {
                pagePointer.Dispose();
                thirdPartyCollectionPagePointers.Remove((userId, collectionId));
                createNewPointer = true;
            }

            if (createNewPointer)
            {
                thirdPartyCollectionPagePointers[(userId, collectionId)] = pagePointer = new LambdaResponsePagePointer<WearableWithDefinitionResponse>(
                    PAGINATED_WEARABLES_END_POINT + $"{userId}?collectionId={collectionId}",
                    pageSize, ct, this);
            }

            var pageResponse = await pagePointer.GetPageAsync(pageNumber, ct);

            if (!pageResponse.success)
                throw new Exception($"The request of the '{collectionId}' third party wearables collection of '{userId}' failed!");

            var wearables = pageResponse.response.wearables.Select(x => x.definition).ToList();
            MapLambdasDataIntoWearableItem(wearables);
            AddWearablesToCatalog(wearables);

            return wearables;
        }

        public async UniTask<WearableItem> RequestWearableAsync(string wearableId, CancellationToken ct)
        {
            if (WearablesCatalog.TryGetValue(wearableId, out WearableItem wearable))
            {
                if (wearablesInUseCounters.ContainsKey(wearableId))
                    wearablesInUseCounters[wearableId]++;

                return wearable;
            }

            ct.ThrowIfCancellationRequested();

            try
            {
                // All the requests happened during the same frames interval are sent together
                return await SyncWearablesRequestsAsync(wearableId, ct);
            }
            catch (OperationCanceledException) { return null; }
        }

        public void AddWearablesToCatalog(IEnumerable<WearableItem> wearableItems)
        {
            foreach (WearableItem wearableItem in wearableItems)
            {
                if (WearablesCatalog.ContainsKey(wearableItem.id))
                    continue;

                wearableItem.SanitizeHidesLists();
                WearablesCatalog.Add(wearableItem.id, wearableItem);

                if (!wearablesInUseCounters.ContainsKey(wearableItem.id))
                    wearablesInUseCounters.Add(wearableItem.id, 1);
            }
        }

        public void RemoveWearablesFromCatalog(IEnumerable<string> wearableIds)
        {
            foreach (string wearableId in wearableIds)
                RemoveWearableFromCatalog(wearableId);
        }

        public void RemoveWearableFromCatalog(string wearableId)
        {
            WearablesCatalog.Remove(wearableId);
            wearablesInUseCounters.Remove(wearableId);
        }

        public void RemoveWearablesInUse(IEnumerable<string> wearablesInUseToRemove)
        {
            foreach (string wearableToRemove in wearablesInUseToRemove)
            {
                if (!wearablesInUseCounters.ContainsKey(wearableToRemove))
                    continue;

                wearablesInUseCounters[wearableToRemove]--;

                if (wearablesInUseCounters[wearableToRemove] > 0)
                    continue;

                WearablesCatalog.Remove(wearableToRemove);
                wearablesInUseCounters.Remove(wearableToRemove);
            }
        }

        public void EmbedWearables(IEnumerable<WearableItem> wearables)
        {
            foreach (WearableItem wearableItem in wearables)
            {
                WearablesCatalog[wearableItem.id] = wearableItem;

                if (wearablesInUseCounters.ContainsKey(wearableItem.id))
                    wearablesInUseCounters[wearableItem.id] = int.MaxValue; //A high value to ensure they are not removed
            }
        }

        public void Clear()
        {
            WearablesCatalog.Clear();
            wearablesInUseCounters.Clear();
            pendingWearablesToRequest.Clear();
        }

        public bool IsValidWearable(string wearableId)
        {
            if (!WearablesCatalog.TryGetValue(wearableId, out var wearable))
                return false;

            return wearable != null;
        }

        UniTask<(WearableWithDefinitionResponse response, bool success)> ILambdaServiceConsumer<WearableWithDefinitionResponse>.CreateRequest
            (string endPoint, int pageSize, int pageNumber, CancellationToken cancellationToken) =>
            lambdasService.Get<WearableWithDefinitionResponse>(
                PAGINATED_WEARABLES_END_POINT,
                endPoint,
                REQUESTS_TIME_OUT_SECONDS,
                ILambdasService.DEFAULT_ATTEMPTS_NUMBER,
                cancellationToken,
                LambdaPaginatedResponseHelper.GetPageSizeParam(pageSize),
                LambdaPaginatedResponseHelper.GetPageNumParam(pageNumber),
                ("includeDefinitions", "true"));

        private async UniTask<WearableItem> SyncWearablesRequestsAsync(string newWearableId, CancellationToken ct)
        {
            pendingWearablesToRequest.Add(newWearableId);
            lastRequestSource ??= new UniTaskCompletionSource<IReadOnlyList<WearableItem>>();
            var sourceToAwait = lastRequestSource;

            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, cancellationToken: ct);

            List<WearableItem> result = new List<WearableItem>();

            if (pendingWearablesToRequest.Count > 0)
            {
                lastRequestSource = null;

                using var wearableIdsPool = PoolUtils.RentList<string>();
                var wearableIds = wearableIdsPool.GetList();
                wearableIds.AddRange(pendingWearablesToRequest);
                pendingWearablesToRequest.Clear();

                // When the number of wearables to request is greater than MAX_WEARABLES_PER_REQUEST, we split the request into several smaller ones.
                // In this way we avoid to send a very long url string that would fail due to the web request size limitations.
                int numberOfPartialRequests = (wearableIds.Count + MAX_WEARABLES_PER_REQUEST - 1) / MAX_WEARABLES_PER_REQUEST;
                var awaitingPartialTasksPool = PoolUtils.RentList<(UniTask<(WearableWithoutDefinitionResponse response, bool success)> task, IEnumerable<string> wearablesRequested)>();
                var awaitingPartialTasks = awaitingPartialTasksPool.GetList();
                for (var i = 0; i < numberOfPartialRequests; i++)
                {
                    int numberOfWearablesToRequest = wearableIds.Count < MAX_WEARABLES_PER_REQUEST
                        ? wearableIds.Count
                        : MAX_WEARABLES_PER_REQUEST;
                    var wearablesToRequest = wearableIds.Take(numberOfWearablesToRequest).ToList();

                    var partialTask = lambdasService.Get<WearableWithoutDefinitionResponse>(
                        NON_PAGINATED_WEARABLES_END_POINT,
                        NON_PAGINATED_WEARABLES_END_POINT,
                        REQUESTS_TIME_OUT_SECONDS,
                        urlEncodedParams: GetWearablesUrlParams(wearablesToRequest),
                        cancellationToken: serviceCts.Token);

                    wearableIds.RemoveRange(0, numberOfWearablesToRequest);
                    awaitingPartialTasks.Add((partialTask, wearablesToRequest));
                }

                var servicePartialResponsesPool = PoolUtils.RentList<((WearableWithoutDefinitionResponse response, bool success) taskResponse, IEnumerable<string> wearablesRequested)>();
                var servicePartialResponses = servicePartialResponsesPool.GetList();

                try
                {
                    foreach (var partialTask in awaitingPartialTasks)
                        servicePartialResponses.Add((await partialTask.task, partialTask.wearablesRequested));
                }
                catch (Exception e)
                {
                    sourceToAwait.TrySetException(e);
                    throw;
                }

                foreach (var partialResponse in servicePartialResponses)
                {
                    if (!partialResponse.taskResponse.success)
                    {
                        Exception e = new Exception($"The request of the wearables ('{string.Join(", ", partialResponse.wearablesRequested)}') failed!");
                        sourceToAwait.TrySetException(e);
                        throw e;
                    }

                    MapLambdasDataIntoWearableItem(partialResponse.taskResponse.response.wearables);
                    AddWearablesToCatalog(partialResponse.taskResponse.response.wearables);
                    result.AddRange(partialResponse.taskResponse.response.wearables);
                }

                sourceToAwait.TrySetResult(result);
            }
            else
                result = (List<WearableItem>)await sourceToAwait.Task;

            ct.ThrowIfCancellationRequested();

            return result.FirstOrDefault(x => x.id == newWearableId);
        }

        private static void MapLambdasDataIntoWearableItem(IEnumerable<WearableItem> wearablesFromLambdas)
        {
            foreach (var wearable in wearablesFromLambdas)
            {
                foreach (var representation in wearable.data.representations)
                {
                    foreach (var representationContent in representation.contents)
                        representationContent.hash = representationContent.url[(representationContent.url.LastIndexOf('/') + 1)..];
                }

                string thumbnail = wearable.thumbnail ?? "";
                int index = thumbnail.LastIndexOf('/');
                string newThumbnail = thumbnail[(index + 1)..];
                string newBaseUrl = thumbnail[..(index + 1)];
                wearable.thumbnail = newThumbnail;
                wearable.baseUrl = string.IsNullOrEmpty(newBaseUrl) ? TEXTURES_URL_ORG : newBaseUrl;
                wearable.baseUrlBundles = ASSET_BUNDLES_URL_ORG;
                wearable.emoteDataV0 = null;
            }
        }

        private static (string paramName, string paramValue)[] GetWearablesUrlParams(IEnumerable<string> wearableIds) =>
            wearableIds.Select(id => ("wearableId", id)).ToArray();
    }
}
