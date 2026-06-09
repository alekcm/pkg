using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapEditorPrototype
{
    public class PlacedObject : MonoBehaviour, IPaintSurfaceOwner
    {
        [SerializeField] private string objectId;
        [SerializeField] private BuildingDefinition definition;
        [SerializeField] private Vector2Int originCell;
        [SerializeField] private int rotationSteps;
        [SerializeField] private float rotationY;
        [SerializeField] private List<Vector2Int> occupiedCells = new List<Vector2Int>();
        [SerializeField] private bool selected;
        [SerializeField] private bool usesGridPlacement = true;
        [SerializeField] private float baseY;

        private Renderer[] cachedRenderers;
        private Collider[] cachedColliders;

        public string ObjectId => objectId;
        public string SurfaceOwnerId => objectId;
        public BuildingDefinition Definition => definition;
        public Vector2Int OriginCell => originCell;
        public int RotationSteps => rotationSteps;
        public float RotationY => rotationY;
        public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;
        public BuildLayer Layer => definition != null ? definition.layer : BuildLayer.Furniture;
        public bool IsSelected => selected;
        public bool UsesGridPlacement => usesGridPlacement;
        public float BaseY => baseY;

        private void Awake()
        {
            CacheComponents();
        }

        public void Initialize(
            BuildingDefinition newDefinition,
            Vector2Int newOriginCell,
            int newRotationSteps,
            float newRotationY,
            List<Vector2Int> newOccupiedCells,
            bool newUsesGridPlacement,
            float newBaseY,
            string newObjectId = null)
        {
            objectId = string.IsNullOrWhiteSpace(newObjectId) ? Guid.NewGuid().ToString("N") : newObjectId;
            definition = newDefinition;
            originCell = newOriginCell;
            rotationSteps = ((newRotationSteps % 4) + 4) % 4;
            rotationY = newRotationY;
            occupiedCells = new List<Vector2Int>(newOccupiedCells);
            usesGridPlacement = newUsesGridPlacement;
            baseY = newBaseY;

            CacheComponents();
            SetSelected(false);

            if (definition != null && definition.layer == BuildLayer.Floor)
            {
                DetailPaintableSurface.EnsureAutoAttached(gameObject, "Floor");
            }
        }

        public void UpdatePlacementData(Vector2Int newOriginCell, int newRotationSteps, float newRotationY, List<Vector2Int> newOccupiedCells, bool newUsesGridPlacement, float newBaseY)
        {
            originCell = newOriginCell;
            rotationSteps = ((newRotationSteps % 4) + 4) % 4;
            rotationY = newRotationY;
            occupiedCells = new List<Vector2Int>(newOccupiedCells);
            usesGridPlacement = newUsesGridPlacement;
            baseY = newBaseY;
        }

        public float GetTopY()
        {
            return GetWorldBounds().max.y;
        }

        public Bounds GetWorldBounds()
        {
            if (cachedColliders == null || cachedColliders.Length == 0)
            {
                CacheComponents();
            }

            bool hasBounds = false;
            Bounds bounds = new Bounds(transform.position, Vector3.zero);

            for (int i = 0; i < cachedColliders.Length; i++)
            {
                Collider colliderComponent = cachedColliders[i];
                if (colliderComponent == null || !colliderComponent.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = colliderComponent.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(colliderComponent.bounds);
                }
            }

            if (hasBounds)
            {
                return bounds;
            }

            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                CacheComponents();
            }

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer rendererComponent = cachedRenderers[i];
                if (rendererComponent == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererComponent.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererComponent.bounds);
                }
            }

            return hasBounds ? bounds : new Bounds(transform.position, Vector3.one * 0.01f);
        }

        public void SetSelected(bool value)
        {
            selected = value;

            if (cachedRenderers == null)
            {
                CacheComponents();
            }

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer rendererComponent = cachedRenderers[i];
                if (rendererComponent == null)
                {
                    continue;
                }

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                rendererComponent.GetPropertyBlock(block);

                if (value)
                {
                    if (RendererSupportsProperty(rendererComponent, "_EmissionColor"))
                    {
                        block.SetColor("_EmissionColor", new Color(0.9f, 0.7f, 0.1f, 1f));
                    }

                    if (RendererSupportsProperty(rendererComponent, "_OutlineColor"))
                    {
                        block.SetColor("_OutlineColor", new Color(1f, 0.8f, 0.1f, 1f));
                    }
                }
                else
                {
                    block.Clear();
                }

                rendererComponent.SetPropertyBlock(block);
            }
        }

        private void CacheComponents()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        private bool RendererSupportsProperty(Renderer rendererComponent, string propertyName)
        {
            Material[] materials = rendererComponent.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material != null && material.HasProperty(propertyName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
