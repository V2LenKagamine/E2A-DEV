using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VRage.Game;
using VRage.ObjectBuilders;

namespace E2A___Ammo_From_Energy.E2A
{
    [ProtoContract(SkipConstructor = true, UseProtoMembersOnly = true)]
    public class E2AState
    {
        private const int CurrentSettingsVersion = 1;

        [ProtoMember(1),XmlElement]
        public int Version { get; set; }

        [ProtoMember(2),XmlElement]
        public SerializableDefinitionId SelectedAmmo { get; set; }

        [ProtoMember(3), XmlElement]
        public SerializableDefinitionId SelectedAmmo2 { get; set; }

        [ProtoMember(4), XmlElement]
        public SerializableDefinitionId SelectedAmmo3 { get; set; }

        [ProtoMember(5), XmlElement]
        public int ToMake { get; set; }

        [ProtoMember(6), XmlElement]
        public int ToMake2 { get; set; }

        [ProtoMember(7), XmlElement]
        public int ToMake3 { get; set; }

        [ProtoMember(8), XmlElement]
        public float SpeedMulti { get; set; }

        [ProtoMember(9), XmlElement]
        public List<MyDefinitionId> AmmoList { get; set; }

        [ProtoMember(10), XmlElement]
        public float powerBuilt { get; set; }

        [ProtoMember(11), XmlElement]
        public float powerBuilt2 { get; set; }

        [ProtoMember(12), XmlElement]
        public float powerBuilt3 { get; set; }

        public E2AState()
        {
            Version = CurrentSettingsVersion;
            SelectedAmmo = MyDefinitionId.Parse("MyObjectBuilder_Ore/Scrap");
            SelectedAmmo2 = MyDefinitionId.Parse("MyObjectBuilder_Ore/Scrap");
            SelectedAmmo3 = MyDefinitionId.Parse("MyObjectBuilder_Ore/Scrap");
            ToMake = 0;
            ToMake2 = 0;
            ToMake3 = 0;
            SpeedMulti = 1.0f;
            AmmoList = new List<MyDefinitionId>();
            powerBuilt = 0f;
            powerBuilt2 = 0f;
            powerBuilt3 = 0f;
        }
    }
}
