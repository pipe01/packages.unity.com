//#define WRITE_LOG

using System;
using UnityEngine;

namespace Unity.Entities
{
    public static class DefaultTinyWorldInitialization
    {
        public static World Initialize(string worldName)
        {
            var world = new World(worldName);
            World.Active = world;

            var allSystemTypes = TypeManager.GetSystems();
            var allSystemNames = TypeManager.SystemNames;

            if (allSystemTypes.Length == 0)
            {
                throw new InvalidOperationException("DefaultTinyWorldInitialization: No Systems found.");
            }

            // Entity manager must be first so that other things can find it.
            world.AddManager(new EntityManager());

            // Create top level presentation system and simulation systems.
            InitializationSystemGroup initializationSystemGroup = new InitializationSystemGroup();
            world.AddManager(initializationSystemGroup);

            SimulationSystemGroup simulationSystemGroup = new SimulationSystemGroup();
            world.AddManager(simulationSystemGroup);

            PresentationSystemGroup presentationSystemGroup = new PresentationSystemGroup();
            world.AddManager(presentationSystemGroup);

            // Create the working set of systems.
            int nSystems = 0;
            Type[] systemTypes = new Type[allSystemTypes.Length];
            ComponentSystem[] systems = new ComponentSystem[allSystemTypes.Length];

#if WRITE_LOG
            Console.WriteLine("--- Adding systems:");
#endif

            for (int i = 0; i < allSystemTypes.Length; i++)
            {
                if (TypeManager.GetSystemAttributes(allSystemTypes[i], typeof(DisableAutoCreationAttribute)).Length > 0)
                    continue;
                if (allSystemTypes[i] == initializationSystemGroup.GetType() ||
                    allSystemTypes[i] == simulationSystemGroup.GetType() ||
                    allSystemTypes[i] == presentationSystemGroup.GetType())
                {
                    continue;
                }

                if (world.GetExistingManager(allSystemTypes[i]) != null)
                    continue;
#if WRITE_LOG
                Console.WriteLine(allSystemNames[i]);
#endif
                systemTypes[nSystems] = allSystemTypes[i];
                systems[nSystems] = TypeManager.ConstructSystem(allSystemTypes[i]);
                world.AddManager(systems[nSystems]);
                nSystems++;
            }
#if WRITE_LOG
            Console.WriteLine("--- Adding systems Done.");
#endif

            for (int i = 0; i < nSystems; ++i)
            {
                var sysType = systemTypes[i];
                var system = systems[i];

                var groups = TypeManager.GetSystemAttributes(sysType, typeof(UpdateInGroupAttribute));
                if (groups.Length == 0)
                {
                    simulationSystemGroup.AddSystemToUpdateList(system);
                }

                for (int g = 0; g < groups.Length; ++g)
                {
                    var groupType = groups[g] as UpdateInGroupAttribute;
                    var groupSystem = world.GetExistingManager(groupType.GroupType) as ComponentSystemGroup;
                    if (groupSystem == null)
                        throw new Exception("DefaultTinyWorldInitialization failed to find existing SystemGroup.");

                    groupSystem.AddSystemToUpdateList(system);
                }
            }

            SortSystems(world);
            return world;
        }

        static public void SortSystems(World world)
        {
            var initializationSystemGroup = world.GetExistingManager<InitializationSystemGroup>();
            var simulationSystemGroup = world.GetExistingManager<SimulationSystemGroup>();
            var presentationSystemGroup = world.GetExistingManager<PresentationSystemGroup>();

            initializationSystemGroup.SortSystemUpdateList();
            simulationSystemGroup.SortSystemUpdateList();
            presentationSystemGroup.SortSystemUpdateList();

#if WRITE_LOG
#if UNITY_ZEROPLAYER
            Console.WriteLine("** Sorted: initializationSystemGroup **");
            initializationSystemGroup.RecursiveLogToConsole();
            Console.WriteLine("** Sorted: simulationSystemGroup **");
            simulationSystemGroup.RecursiveLogToConsole();
            Console.WriteLine("** Sorted: presentationSystemGroup **");
            presentationSystemGroup.RecursiveLogToConsole();
            Console.WriteLine("** Sorted done. **");
#endif
#endif
        }
    }
}
