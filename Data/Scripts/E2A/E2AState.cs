using ProtoBuf;
using System.Collections.Generic;
using System.Xml.Serialization;
using VRage.Game;
using VRage.ObjectBuilders;

namespace E2A___Ammo_From_Energy.E2A
{
    [ProtoContract(SkipConstructor = true, UseProtoMembersOnly = true)]
    public class E2AState
    {
        private const int CurrentSettingsVersion = 2;

        [ProtoMember(1),XmlElement]
        public int Version { get; set; }

        [ProtoMember(2), XmlElement]
        public float SpeedMulti { get; set; }

        [ProtoMember(3), XmlElement]
        public List<MyDefinitionId> AmmoList { get; set; }

        [ProtoMember(4), XmlElement]
        public List<E2AData> DataList { get; set; }

        public E2AState()
        {
            Version = CurrentSettingsVersion;
            SpeedMulti = 1.0f;
            AmmoList = new List<MyDefinitionId>();
            DataList = new List<E2AData>() { new E2AData(), new E2AData(), new E2AData() };
        }
    }

    public class E2AData
    {
        public SerializableDefinitionId SelectedAmmoType = MyDefinitionId.Parse("MyObjectBuilder_Ore/Scrap");
        public int AmtToMake = -1;
        public float PowerStored= -1;

        public E2AData(SerializableDefinitionId selectedAmmoType, int amtToMake, float powerStored)
        {
            SelectedAmmoType = selectedAmmoType;
            AmtToMake = amtToMake;
            PowerStored = powerStored;
        }
        public E2AData() { }
    }
}
