using System;
using Unity.MemoryProfiler.Editor.Database.Operation.Filter;
using UnityEditor;
using UnityEngine;

namespace Unity.MemoryProfiler.Editor.UI.Treemap
{
    public interface IMetricValue
    {
        string GetName();
        long GetValue();
        string GetGroupName();
        int GetObjectUID();
    }
    public class ManagedObjectMetric : IMetricValue
    {
        public CachedSnapshot m_Snapshot;
        public ManagedObjectInfo m_Object;
        public ManagedObjectMetric(CachedSnapshot snapshot, ManagedObjectInfo obj)
        {
            m_Snapshot = snapshot;
            m_Object = obj;
        }

        public string GetTypeName()
        {
            if (m_Object.iTypeDescription >= 0)
            {
                string typeName = m_Snapshot.typeDescriptions.typeDescriptionName[m_Object.iTypeDescription];
                return typeName;
            }
            return "<unknown managed type>";
        }

        public string GetName()
        {
            if (m_Object.nativeObjectIndex >= 0)
            {
                string objName = m_Snapshot.nativeObjects.objectName[m_Object.nativeObjectIndex];
                if (objName.Length > 0)
                {
                    return " \"" + objName + "\" <" + GetTypeName() + ">";
                }
            }
            return string.Format("[0x{0:x16}]", m_Object.ptrObject) + " < " + GetTypeName() + " > ";
        }

        public bool IsSame(IMetricValue obj)
        {
            if (obj is ManagedObjectMetric)
            {
                var o = (ManagedObjectMetric)obj;
                return o.m_Object == m_Object;
            }
            return false;
        }

        string IMetricValue.GetName()
        {
            return this.GetName();
        }

        long IMetricValue.GetValue()
        {
            return m_Object.size;
        }

        string IMetricValue.GetGroupName()
        {
            return this.GetTypeName();
        }

        int IMetricValue.GetObjectUID()
        {
            return m_Snapshot.ManagedObjectIndexToUnifiedObjectIndex(m_Object.managedObjectIndex);
        }
    }
    public class NativeObjectMetric : IMetricValue
    {
        public CachedSnapshot m_Snapshot;
        public int m_ObjectIndex;
        public NativeObjectMetric(CachedSnapshot snapshot, int objectIndex)
        {
            m_Snapshot = snapshot;
            m_ObjectIndex = objectIndex;
        }

        public bool IsSame(IMetricValue obj)
        {
            if (obj is NativeObjectMetric)
            {
                var o = (NativeObjectMetric)obj;
                return o.m_ObjectIndex == m_ObjectIndex;
            }
            return false;
        }

        public string GetTypeName()
        {
            if (m_Snapshot.nativeObjects.nativeTypeArrayIndex[m_ObjectIndex] > 0)
            {
                var typeName = m_Snapshot.nativeTypes.typeName[m_Snapshot.nativeObjects.nativeTypeArrayIndex[m_ObjectIndex]];
                return typeName;
            }
            return "<unknown native type>";
        }

        public string GetName()
        {
            string objectName = m_Snapshot.nativeObjects.objectName[m_ObjectIndex];
            if (objectName.Length > 0)
            {
                return " \"" + objectName + "\" <" + GetTypeName() + ">";
            }
            else
            {
                return GetTypeName();
            }
        }

        string IMetricValue.GetName()
        {
            return this.GetName();
        }

        long IMetricValue.GetValue()
        {
            return (long)m_Snapshot.nativeObjects.size[m_ObjectIndex];
        }

        string IMetricValue.GetGroupName()
        {
            return this.GetTypeName();
        }

        int IMetricValue.GetObjectUID()
        {
            return m_Snapshot.NativeObjectIndexToUnifiedObjectIndex(m_ObjectIndex);
        }
    }
    public class Item : IComparable<Item>, ITreemapRenderable
    {
        public Group _group;
        public Rect _position;
        public int _index;

        public IMetricValue _metric;

        public long value { get { return _metric.GetValue(); } }
        public string name { get { return _metric.GetName(); } }
        public Color color { get { return _group.color; } }

        public Item(IMetricValue metric, Group group)
        {
            _metric = metric;
            _group = group;
        }

        public int CompareTo(Item other)
        {
            return (int)(_group != other._group ? other._group.totalValue - _group.totalValue : other.value - value);
        }

        public Color GetColor()
        {
            return _group.color;
        }

        public Rect GetPosition()
        {
            return _position;
        }

        public string GetLabel()
        {
            string row1 = _group._name;
            string row2 = EditorUtility.FormatBytes(value);
            return row1 + "\n" + row2;
        }

        public CachedSnapshot m_Snapshot;
    }
}
