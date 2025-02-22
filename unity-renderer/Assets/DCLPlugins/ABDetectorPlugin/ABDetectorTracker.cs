using System;
using System.Collections.Generic;
using DCL.Controllers;
using UnityEngine;

namespace DCL
{
    public class ABDetectorTracker : IDisposable
    {
        private const string FROM_ASSET_BUNDLE_TAG = "FromAssetBundle";
        private const string FROM_RAW_GLTF_TAG = "FromRawGLTF";
        private const string PARAMETRIZED_SHAPE_TAG = "FromParametrized";
        private const string AB_DETECTOR_MATERIALS_PREFAB_NAME = "AbDetectorMaterials";

        private readonly DebugConfig debugConfig;
        private readonly DataStore_Player player;

        private readonly Dictionary<Renderer, Material[]> rendererDict = new Dictionary<Renderer, Material[]>();
        private readonly Multimap<IParcelScene, Renderer> parcelToRendererMultimap = new Multimap<IParcelScene, Renderer>();

        private ABDetectorMaterialsHolder abDetectorMaterialsHolder;
        private readonly IWorldState worldState;

        
        public ABDetectorTracker(DebugConfig debugConfig, DataStore_Player player, IWorldState worldState)
        {
            this.debugConfig = debugConfig;
            this.player = player;
            this.worldState = worldState;

            debugConfig.showGlobalABDetectionLayer.OnChange += OnGlobalABDetectionChanged;
            debugConfig.showSceneABDetectionLayer.OnChange += OnSceneABDetectionChanged;
        }

        public void Dispose()
        {
            debugConfig.showGlobalABDetectionLayer.OnChange -= OnGlobalABDetectionChanged;
            debugConfig.showSceneABDetectionLayer.OnChange -= OnSceneABDetectionChanged;
        }
        

        private IParcelScene FindSceneForPlayer()
        {
            Vector2Int currentPos = player.playerGridPosition.Get();
            return worldState.TryGetScene(worldState.GetSceneNumberByCoords(currentPos), out IParcelScene resultScene)
                ? resultScene
                : null;
        }

        private void LoadMaterialsIfNeeded()
        {
            if (abDetectorMaterialsHolder == null)
            {
                abDetectorMaterialsHolder = Resources.Load<GameObject>(AB_DETECTOR_MATERIALS_PREFAB_NAME).
                    GetComponent<ABDetectorMaterialsHolder>();
            }
        }

        private void OnGlobalABDetectionChanged(bool current, bool previous)
        {
            LoadMaterialsIfNeeded();

            if (current)
            {
                RemoveGlobalABDetectionPainting();
                ApplyGlobalABDetectionPainting();
            }
            else
                RemoveGlobalABDetectionPainting();
        }

        private void OnSceneABDetectionChanged(bool current, bool previous)
        {
            LoadMaterialsIfNeeded();

            if (current)
            {
                RemoveGlobalABDetectionPainting();
                ApplyABDetectionPaintingForCurrentScene();
            }
            else
                RemoveABDetectionPaintingForCurrentScene();
        }

        private void ApplyGlobalABDetectionPainting()
        {
            UnityEngine.Object[] gameObjects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject));
            foreach (var gameObject in gameObjects)
            {
                var converted = (GameObject)gameObject;
                if (converted.transform.parent == null)
                    ApplyMaterials(converted.transform);
            }
        }

        private void RemoveGlobalABDetectionPainting()
        {
            foreach (KeyValuePair<Renderer, Material[]> keyValuePair in rendererDict)
            {
                if (keyValuePair.Key != null)
                    keyValuePair.Key.materials = keyValuePair.Value;
            }

            rendererDict.Clear();
            parcelToRendererMultimap.Clear();
        }

        private void ApplyABDetectionPaintingForCurrentScene()
        {
            var currentScene = FindSceneForPlayer();
            ApplyMaterials(currentScene.GetSceneTransform(), currentScene);
        }

        private void RemoveABDetectionPaintingForCurrentScene()
        {
            var currentScene = FindSceneForPlayer();
            if (parcelToRendererMultimap.ContainsKey(currentScene))
            {
                foreach (var renderer in parcelToRendererMultimap.GetValues(currentScene))
                {
                    if (rendererDict.TryGetValue(renderer, out var materials))
                        renderer.materials = materials;

                    rendererDict.Remove(renderer);
                }

                parcelToRendererMultimap.Remove(currentScene);
            }
        }

        private void ApplyMaterials(Transform someTransform, IParcelScene optionalParcelScene = null)
        {
            if (someTransform.childCount > 0)
            {
                for (int i = 0; i < someTransform.childCount; i++)
                {
                    Transform childTransform = someTransform.GetChild(i).transform;
                    Renderer[] renderers = childTransform.GetComponents<Renderer>();

                    foreach (Renderer renderer in renderers)
                    {
                        rendererDict[renderer] = renderer.materials;

                        if (optionalParcelScene != null)
                            parcelToRendererMultimap.Add(optionalParcelScene, renderer);

                        if (renderer.CompareTag(FROM_ASSET_BUNDLE_TAG))
                            renderer.material = abDetectorMaterialsHolder.ABMaterial;
                        else if (renderer.CompareTag(FROM_RAW_GLTF_TAG))
                            renderer.material = abDetectorMaterialsHolder.GLTFMaterial;
                        else if (renderer.CompareTag(PARAMETRIZED_SHAPE_TAG))
                            renderer.material = abDetectorMaterialsHolder.ParametrizedShapeMaterial;
                    }

                    ApplyMaterials(childTransform, optionalParcelScene);
                }
            }
        }
    }
}
