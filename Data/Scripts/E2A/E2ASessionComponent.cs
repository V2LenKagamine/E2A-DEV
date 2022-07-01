
using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace E2A___Ammo_From_Energy.E2A
{


    [ProtoContract]
    public class E2ASettings
    {

        [ProtoMember(1)]
        public float m_PowerMulti;

        public void Validate()
        {
            m_PowerMulti = MathHelper.Clamp(m_PowerMulti, .05f, 100f);
        }

    }

    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class E2ASession : MySessionComponentBase
    {

        public static E2ASession Static { get; private set; }

        public E2ASettings Settings;

        private static E2ALogic E2ALogicClass = new E2ALogic();


        public override void BeforeStart()
        {
            LoadSettings();

        }

        protected override void UnloadData()
        {
            base.UnloadData();
            Static = null;
        }
        private void LoadSettings()
        {
            if (!MyAPIGateway.Session.IsServer)
            {
                Settings = new E2ASettings();
                return;
            }

            if (MyAPIGateway.Utilities.FileExistsInWorldStorage("Settings.xml", typeof(E2ASettings)) == true)
            {
                try
                {
                    var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage("Settings.xml", typeof(E2ASettings));
                    Settings = MyAPIGateway.Utilities.SerializeFromXML<E2ASettings>(reader.ReadToEnd());
                    Settings.Validate();
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLine("Len.ERROR:" + e);
                }
            }

            if (Settings == null)
            {
                Settings = new E2ASettings();
                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            if (!Util.IsServer)
                return;

            using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("Settings.xml", typeof(E2ASettings)))
            {
                writer.Write(MyAPIGateway.Utilities.SerializeToXML<E2ASettings>(Settings));
            }
        }

    }

}
