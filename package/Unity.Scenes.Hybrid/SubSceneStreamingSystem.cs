using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;
using UnityEngine.Profiling;
using Hash128 = UnityEngine.Hash128;

namespace Unity.Scenes
{
    [ExecuteAlways]
    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class SubSceneStreamingSystem : ComponentSystem
    {
        public struct IgnoreTag : IComponentData
        {
            
        }
       
        
        internal enum StreamingStatus
        {
            NotYetProcessed,
            Loaded,
            Loading,
            FailedToLoad
        }

        internal struct StreamingState : ISystemStateComponentData
        {
            public StreamingStatus Status;
            public int            ActiveStreamIndex;
            public int            LoadedFromLiveLink;
        }

        struct Stream
        {
            public World                   World;
            public Entity                  SceneEntity;
            public AsyncLoadSceneOperation Operation;
        }

        const int LoadScenesPerFrame = 4;
        int MaximumMoveEntitiesFromPerFrame = 1;
        
        Stream[] m_Streams = new Stream[LoadScenesPerFrame];
        ComponentGroup m_PendingStreamRequests;
        ComponentGroup m_UnloadStreamRequests;
        ComponentGroup m_SceneFilter;
        ComponentGroup m_SceneFilterPrefabs;
        ComponentGroup m_PublicRefFilter;
        ComponentGroup m_SectionData;

        ProfilerMarker m_MoveEntitiesFrom = new ProfilerMarker("SceneStreaming.MoveEntitiesFrom");
        ProfilerMarker m_ExtractEntityRemapRefs = new ProfilerMarker("SceneStreaming.ExtractEntityRemapRefs");
        ProfilerMarker m_AddSceneSharedComponents = new ProfilerMarker("SceneStreaming.AddSceneSharedComponents");

        protected override void OnCreateManager()
        {
            for (int i = 0; i < LoadScenesPerFrame; ++i)
                CreateStreamWorld(i);
            
            m_PendingStreamRequests = GetComponentGroup(new EntityArchetypeQuery()
            {
                All = new[] {ComponentType.ReadWrite<RequestSceneLoaded>(), ComponentType.ReadWrite<SceneData>()},
                None = new[] {ComponentType.ReadWrite<StreamingState>(), ComponentType.ReadWrite<IgnoreTag>() }
            });

            m_UnloadStreamRequests = GetComponentGroup(new EntityArchetypeQuery()
            {
                All = new[] {ComponentType.ReadWrite<StreamingState>()},
                None = new[] {ComponentType.ReadWrite<RequestSceneLoaded>(), ComponentType.ReadWrite<IgnoreTag>()}
            });

            m_PublicRefFilter = GetComponentGroup
            (
                ComponentType.ReadWrite<SceneTag>(),
                ComponentType.ReadWrite<PublicEntityRef>()
            );

            m_SectionData = GetComponentGroup
            (
                ComponentType.ReadWrite<SceneData>()
            );

            //@TODO: Not handling inactive objects...
            //@TODO: Shared component filtering is not supported when there is no EntityArchetypeQuery...
            m_SceneFilter = GetComponentGroup(ComponentType.ReadWrite<SceneTag>() );
            m_SceneFilterPrefabs = GetComponentGroup(ComponentType.ReadWrite<SceneTag>(), ComponentType.ReadWrite<Prefab>() );
        }

        protected override void OnDestroyManager()
        {
            for (int i = 0; i != m_Streams.Length; i++)
            {
                m_Streams[i].Operation?.Dispose();
                DestroyStreamWorld(i);
            }
        }
        
        void DestroyStreamWorld(int index)
        {
            m_Streams[index].World.Dispose();
            m_Streams[index].World = null;
            m_Streams[index].Operation = null;
        }

        void CreateStreamWorld(int index)
        {
            m_Streams[index].World = new World("LoadingWorld" + index);
            m_Streams[index].World.CreateManager<EntityManager>();
        }

        static NativeArray<Entity> GetExternalRefEntities(EntityManager manager, Allocator allocator)
        {
            using (var group = manager.CreateComponentGroup(typeof(ExternalEntityRefInfo)))
            {
                return group.ToEntityArray(allocator);
            }
        }

        //@TODO There must be a more efficient way
        static Entity GetPublicRefEntity(EntityManager manager, Entities.Hash128 guid)
        {
            using (var sceneDataGrp = manager.CreateComponentGroup(typeof(SceneData)))
            {
                using (var sceneEntities = sceneDataGrp.ToEntityArray(Allocator.TempJob))
                {
                    foreach (var sceneEntity in sceneEntities)
                    {
                        var scenedata = manager.GetComponentData<SceneData>(sceneEntity);
                        if (scenedata.SceneGUID == guid && scenedata.SubSectionIndex == 0)
                        {
                            using(var group = manager.CreateComponentGroup(typeof(SceneTag), typeof(PublicEntityRef)))
                            {
                                group.SetFilter(new SceneTag {SceneEntity = sceneEntity});
                                using (var entities = group.ToEntityArray(Allocator.TempJob))
                                {
                                    return entities.Length > 0 ? entities[0] : Entity.Null;
                                }
                            }
                        }
                    }
                }
            }

            return Entity.Null;
        }

        NativeArray<SceneTag> ExternalRefToSceneTag(NativeArray<ExternalEntityRefInfo> externalEntityRefInfos, Allocator allocator)
        {
            var sceneTags = new NativeArray<SceneTag>(externalEntityRefInfos.Length, allocator);

            using (var sectionDataEntities = m_SectionData.ToEntityArray(Allocator.TempJob))
            {
                using (var sectionData = m_SectionData.ToComponentDataArray<SceneData>(Allocator.TempJob))
                {
                    for (int i = 0; i < sectionData.Length; ++i)
                    {
                        for (int j = 0; j < externalEntityRefInfos.Length; ++j)
                        {
                            if (
                                externalEntityRefInfos[j].SceneGUID == sectionData[i].SceneGUID
                                &&
                                externalEntityRefInfos[j].SubSectionIndex == sectionData[i].SubSectionIndex
                            )
                            {
                                sceneTags[j] = new SceneTag {SceneEntity = sectionDataEntities[i]};
                                break;
                            }
                        }
                    }
                }
            }

            return sceneTags;
        }

        bool MoveEntities(EntityManager srcManager, Entity sceneEntity)
        {
            NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping;
            using (m_ExtractEntityRemapRefs.Auto())
            {
                if (!ExtractEntityRemapRefs(srcManager, out entityRemapping)) 
                    return false;
            }

            using (m_AddSceneSharedComponents.Auto())
            {
#if UNITY_EDITOR
                var data = new EditorRenderData()
                {
                    SceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.DefaultSceneCullingMask | (1UL << 59),
                    PickableObject = EntityManager.HasComponent<SubScene>(sceneEntity) ? EntityManager.GetComponentObject<SubScene>(sceneEntity).gameObject : null
                };
                srcManager.AddSharedComponentData(srcManager.UniversalGroup, data);
#endif
            
                srcManager.AddSharedComponentData(srcManager.UniversalGroup, new SceneTag { SceneEntity = sceneEntity});
            }


            using (m_MoveEntitiesFrom.Auto())
            {
                EntityManager.MoveEntitiesFrom(srcManager, entityRemapping);
            }

            entityRemapping.Dispose();
            srcManager.PrepareForDeserialize();

            return true;
        }

        bool ExtractEntityRemapRefs(EntityManager srcManager, out NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping)
        {
            // External entity references are "virtual" entities. If we don't have any, only real entities need remapping
            int remapTableSize = srcManager.EntityCapacity;

            using (var externalRefEntities = GetExternalRefEntities(srcManager, Allocator.TempJob))
            {
                // We can potentially have several external entity reference arrays, each one pointing to a different scene
                var externalEntityRefInfos = new NativeArray<ExternalEntityRefInfo>(externalRefEntities.Length, Allocator.Temp);
                for (int i = 0; i < externalRefEntities.Length; ++i)
                {
                    // External references point to indices beyond the range used by the entities in this scene
                    // The highest index used by all those references defines how big the remap table has to be
                    externalEntityRefInfos[i] = srcManager.GetComponentData<ExternalEntityRefInfo>(externalRefEntities[i]);
                    var extRefs = srcManager.GetBuffer<ExternalEntityRef>(externalRefEntities[i]);
                    remapTableSize = math.max(remapTableSize, externalEntityRefInfos[i].EntityIndexStart + extRefs.Length);
                }

                // Within a scene, external scenes are identified by some ID
                // In the destination world, scenes are identified by an entity
                // Every entity coming from a scene needs to have a SceneTag that references the scene entity
                using (var sceneTags = ExternalRefToSceneTag(externalEntityRefInfos, Allocator.TempJob))
                {
                    entityRemapping = new NativeArray<EntityRemapUtility.EntityRemapInfo>(remapTableSize, Allocator.TempJob);

                    for (int i = 0; i < externalRefEntities.Length; ++i)
                    {
                        var extRefs = srcManager.GetBuffer<ExternalEntityRef>(externalRefEntities[i]);
                        var extRefInfo = srcManager.GetComponentData<ExternalEntityRefInfo>(externalRefEntities[i]);

                        // A scene that external references point to is expected to have a single public reference array
                        m_PublicRefFilter.SetFilter(sceneTags[i]);
                        using (var pubRefEntities = m_PublicRefFilter.ToEntityArray(Allocator.TempJob))
                        {
                            if (pubRefEntities.Length == 0)
                            {
                                // If the array is missing, the external scene isn't loaded, we have to wait.
                                entityRemapping.Dispose();
                                return false;
                            }

                            var pubRefs = EntityManager.GetBuffer<PublicEntityRef>(pubRefEntities[0]);

                            // Proper mapping from external reference in section to entity in main world
                            for (int k = 0; k < extRefs.Length; ++k)
                            {
                                var srcIdx = extRefInfo.EntityIndexStart + k;
                                var target = pubRefs[extRefs[k].entityIndex].targetEntity;

                                // External references always have a version number of 1
                                entityRemapping[srcIdx] = new EntityRemapUtility.EntityRemapInfo
                                {
                                    SourceVersion = 1,
                                    Target = target
                                };
                            }
                        }

                        m_PublicRefFilter.ResetFilter();
                    }
                }
            }

            return true;
        }

        bool ProcessActiveStreams()
        {
            bool needsMoreProcessing = false;
            int moveEntitiesFromProcessed = 0;
            
            for (int i = 0; i != m_Streams.Length; i++)
            {
                var operation = m_Streams[i].Operation;
                var streamingWorld = m_Streams[i].World;
                var streamingManager = streamingWorld.GetOrCreateManager<EntityManager>();
                
                if (operation != null)
                {
                    operation.Update();
                    needsMoreProcessing = true;

                    if (operation.IsCompleted && moveEntitiesFromProcessed < MaximumMoveEntitiesFromPerFrame)
                    {
                        // Loading failed, EntityManager is in unknown state. Just wipe it out and create a clean one.
                        if (operation.ErrorStatus == null)
                        {
                            streamingManager.EndExclusiveEntityTransaction();

                            if (EntityManager.HasComponent<RequestSceneLoaded>(m_Streams[i].SceneEntity))
                            {
                                if (MoveEntities(streamingManager, m_Streams[i].SceneEntity))
                                {
                                    m_Streams[i].Operation.Dispose();
                                    m_Streams[i].Operation = null;

                                    var state = EntityManager.GetComponentData<StreamingState>(m_Streams[i].SceneEntity);
                                    state.Status = StreamingStatus.Loaded;
                                    EntityManager.SetComponentData(m_Streams[i].SceneEntity, state);
                                    moveEntitiesFromProcessed++;
                                }
                                else
                                {
                                    // Debug.Log("MoveEntities on hold, waiting for main section");
                                }
                            }
                            // The SubScene is no longer being requested for load
                            else
                            {
                                m_Streams[i].Operation.Dispose();
                                m_Streams[i].Operation = null;

                                EntityManager.RemoveComponent<StreamingState>(m_Streams[i].SceneEntity);

                                streamingManager.DestroyEntity(streamingManager.UniversalGroup);
                                streamingManager.PrepareForDeserialize();
                                moveEntitiesFromProcessed++;
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Error when processing '{operation}': {operation.ErrorStatus}");

                            DestroyStreamWorld(i);
                            CreateStreamWorld(i);

                            // If load fails, don't try to load the requestScene again.
                            var state = EntityManager.GetComponentData<StreamingState>(m_Streams[i].SceneEntity);
                            state.Status = StreamingStatus.FailedToLoad;
                            EntityManager.SetComponentData(m_Streams[i].SceneEntity, state);
                            moveEntitiesFromProcessed++;
                        }
                    }
                }
            }

            return needsMoreProcessing;
        }

        protected override void OnUpdate()
        {
            var destroySubScenes = new NativeList<Entity>(Allocator.Temp);
            
            var commands = new EntityCommandBuffer(Allocator.Temp);
            ForEach((Entity entity) =>
            {
                var streamIndex = CreateAsyncLoadScene(entity);
                if (streamIndex != -1)
                {
                    var streamingState = new StreamingState { ActiveStreamIndex = streamIndex, Status = StreamingStatus.NotYetProcessed};
                    commands.AddComponent(entity, streamingState);
                }
            }, m_PendingStreamRequests);
            commands.Playback(EntityManager);
            commands.Dispose();
            

            ForEach((Entity entity) =>
            {
                destroySubScenes.Add(entity);
            }, m_UnloadStreamRequests);

            foreach (var destroyScene in destroySubScenes.AsArray())
                UnloadSceneImmediate(destroyScene);

            if (ProcessActiveStreams())
                EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        public void UnloadSceneImmediate(Entity scene)
        {
            if (EntityManager.HasComponent<StreamingState>(scene))
            {                          
                m_SceneFilter.SetFilter(new SceneTag {SceneEntity = scene });
                                
                EntityManager.DestroyEntity(m_SceneFilter);
                EntityManager.DestroyEntity(m_SceneFilterPrefabs);
                
                m_SceneFilter.ResetFilter();
            
                EntityManager.RemoveComponent<StreamingState>(scene);
            }
        }
        
        int CreateAsyncLoadScene(Entity entity)
        {
            for (int i = 0; i != m_Streams.Length; i++)
            {
                if (m_Streams[i].Operation != null)
                    continue;

                var sceneData = EntityManager.GetComponentData<SceneData>(entity);
                
                var entitiesBinaryPath = EntityScenesPaths.GetLoadPath(sceneData.SceneGUID, EntityScenesPaths.PathType.EntitiesBinary, sceneData.SubSectionIndex);
                var resourcesPath = EntityScenesPaths.GetLoadPath(sceneData.SceneGUID, EntityScenesPaths.PathType.EntitiesSharedComponents, sceneData.SubSectionIndex);
                var entityManager = m_Streams[i].World.GetOrCreateManager<EntityManager>(); 

                m_Streams[i].Operation = new AsyncLoadSceneOperation(entitiesBinaryPath, sceneData.FileSize, sceneData.SharedComponentCount, resourcesPath, entityManager);
                m_Streams[i].SceneEntity = entity;
                return i;
            }

            return -1;
        }
        
        public static void MarkDrivenByLiveLink(EntityManager manager, Entity sceneEntity)
        {
            if (!manager.HasComponent<StreamingState>(sceneEntity))
                manager.AddComponentData(sceneEntity, new StreamingState { Status = StreamingStatus.Loaded});
            else
                manager.SetComponentData(sceneEntity, new StreamingState { Status = StreamingStatus.Loaded});
            
            if (!manager.HasComponent<IgnoreTag>(sceneEntity))
                manager.AddComponentData(sceneEntity, new IgnoreTag());
        }
    }
}
