﻿using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AssetGroupTests : AddressableAssetTestBase
    {
        [Test]
        public void AddRemoveEntry()
        {
            var group = m_Settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            Assert.IsNotNull(group);
            var entry = new AddressableAssetEntry(m_AssetGUID, "test", group, false);
            group.AddAssetEntry(entry);
            Assert.IsNotNull(group.GetAssetEntry(m_AssetGUID));
            group.RemoveAssetEntry(entry);
            Assert.IsNull(group.GetAssetEntry(m_AssetGUID));
        }

        [Test]
        public void RenameSlashesBecomeDashes()
        {
            var group = m_Settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            var oldName = group.Name;
            group.Name = "folder/name";
            Assert.AreEqual("folder-name", group.Name);
            group.Name = oldName;
        }
        [Test]
        public void RenameInvalidCharactersFails()
        {
            var group = m_Settings.FindGroup(AddressableAssetSettings.DefaultLocalGroupName);
            var oldName = group.Name;
            string badName = "*#?@>!@*@(#";
            LogAssert.Expect(LogType.Error, "Rename of Group failed. Invalid file name: '" + badName + ".asset'");
            group.Name = badName;
            Assert.AreEqual(oldName, group.Name);
        }

        [Test]
        public void DedupeEntries_WhenGroupsHaveOverlappingAssetEntries_RemovesEntries()
        {
            const string guid = "0000";
            const string address = "not/a/real/address";
            AddressableAssetGroup group1 = m_Settings.CreateGroup("group1", false, false, true, null, new Type[] { });
            AddressableAssetGroup group2 = m_Settings.CreateGroup("group2", false, false, true, null, new Type[] { });

            //We're making 2 identical enteries.  This is to simulate each group having it's own copy of an AA Entry that references the same object.
            //If we use the same object the call to AddAssetEntry won't give us the state we're looking for.
            AddressableAssetEntry entry = new AddressableAssetEntry(guid, address, group1, false);
            AddressableAssetEntry entry2 = new AddressableAssetEntry(guid, address, group2, false);

            group1.AddAssetEntry(entry);
            group2.AddAssetEntry(entry2);

            //Ensuring our setup is correct
            Assert.IsNotNull(group1.GetAssetEntry(guid));
            Assert.IsNotNull(group2.GetAssetEntry(guid));

            group1.DedupeEnteries(); //We setup our entry with group1 so it should retain its reference
            group2.DedupeEnteries(); //The entry was added to group2 afterwards and should lose its reference

            Assert.IsNotNull(group1.GetAssetEntry(guid));
            Assert.IsNull(group2.GetAssetEntry(guid));

            //Cleanup
            m_Settings.RemoveGroup(group1);
            m_Settings.RemoveGroup(group2);
        }

        [Test]
        public void RemoveEntries_InvokesModificationNotification()
        {
            AddressableAssetGroup group1 = m_Settings.CreateGroup("group1", false, false, true, null, new Type[] { });

            List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();
            for (int i = 0; i < 10; i++)
                group1.AddAssetEntry(new AddressableAssetEntry("000" + i.ToString(), "unknown" + i.ToString(), group1, false));
        

            List<AddressableAssetEntry> callbackEntries = new List<AddressableAssetEntry>();
            Action<AddressableAssetSettings, AddressableAssetSettings.ModificationEvent, object> callback = (x, y, z) => callbackEntries.AddRange((AddressableAssetEntry[])z);
            AddressableAssetSettings.OnModificationGlobal += callback;

            group1.RemoveAssetEntries(entries.ToArray());

            for(int i = 0 ; i < entries.Count; i++)
                Assert.AreEqual(entries[i], callbackEntries[i]);

            //Cleanup
            AddressableAssetSettings.OnModificationGlobal -= callback;
            m_Settings.RemoveGroup(group1);
        }
    }
}