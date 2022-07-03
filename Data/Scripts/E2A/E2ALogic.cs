using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using static Sandbox.Definitions.MyBlueprintDefinitionBase;
using VRage.Utils;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using VRage.Network;
using VRage.Game.ModAPI.Network;
using VRage.Sync;
using VRage.Game.Entity;

namespace E2A___Ammo_From_Energy.E2A
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "SE2ABlock", "BE2ABlock")]
    public class E2ALogic : MyGameLogicComponent, IMyEventProxy
    {

        public static readonly Guid ModStorageID = new Guid("02a4c5ec-769a-4592-92a8-2a2f33cae9c1");

        public bool IsTerminalOpen { get; set; }

        public static List<IMyTerminalControl> m_customControls = new List<IMyTerminalControl>();
        public static List<MyDefinitionId> m_AmmoIdSlot = new List<MyDefinitionId>();
        public static Dictionary<MyDefinitionId, Item> m_BPtoAmmo = new Dictionary<MyDefinitionId, Item>();
        public static List<string> m_AmmoNames = new List<string>();
        private static Dictionary<MyDefinitionId, float> m_AvailAmmo = new Dictionary<MyDefinitionId, float>();


        public List<MyInventoryItem> m_Inv = new List<MyInventoryItem>();
        private MyFixedPoint itemTotal;
        public static float m_PowerMulti
        {
            get { return E2ASession.Static?.Settings.m_PowerMulti ?? 1f; }
        }
        private IMyFunctionalBlock m_block;

        private float m_lastRequiredInput;
        private float m_OperationPower;
        private float m_StandbyPower;
        private bool m_isPowered;
        private static bool m_ControlsCreated = false;

        public MySync<float, SyncDirection.FromServer> mess_BuiltPower = null;
        public MySync<float, SyncDirection.BothWays> mess_SpeedMulti = null;
        public MySync<int, SyncDirection.BothWays> mess_toMake = null;
        public MySync<int, SyncDirection.BothWays> mess_SelectedAmmo = null;
        /*
        public float m_SpeedMulti;
        public int m_toMake;
        public int m_SelectedAmmo;
        */

        private int LastRunTick = -1;


        public float requiredPowerMulti
        {
            get { return (mess_SpeedMulti.Value * 1.25f) * m_PowerMulti; }
        }

        private bool IsWorking
        {
            get { return m_block.Enabled && m_block.IsFunctional && mess_SelectedAmmo.Value != 0; }
        }

        private float RequiredOperationalPower
        {
            get { return m_OperationPower * m_PowerMulti * mess_SpeedMulti.Value; }

        }
        public override void Close()
        {
            ((MyResourceSinkComponent)m_block.ResourceSink).CurrentInputChanged -= OnPowerInputChanged;
        }

        private float PowerReq()
        {
            if (!m_block.Enabled || !m_block.IsFunctional)
            {
                return 0f;
            }
            return Math.Max(m_StandbyPower, RequiredOperationalPower);
        }

        public override void UpdateAfterSimulation()
        {
            Float();
        }


        public override void UpdateOnceBeforeFrame()
        {
            var sink = Entity.Components.Get<MyResourceSinkComponent>();
            //MyLog.Default.WriteLine("Len.Loading Last State");
            LoadState();
            SaveState();

            //mess_SelectedAmmo = null;
            //MyLog.Default.WriteLine("Len.AmmoChanged = " + mess_SelectedAmmo);
            mess_SelectedAmmo.ValueChanged += OnMessage_AmmoChanged;

            //mess_SpeedMulti = null;
            //MyLog.Default.WriteLine("Len.SpeedChanged = " + mess_SpeedMulti);
            mess_SpeedMulti.ValueChanged += OnMessage_SpeedChanged;

            //mess_SpeedMulti = null;
            //MyLog.Default.WriteLine("Len.MakeChanged = " + mess_toMake);
            mess_toMake.ValueChanged += OnMessage_MakeChanged;

            if (sink != null)
            {
                sink.SetRequiredInputFuncByType(MyResourceDistributorComponent.ElectricityId, PowerReq);
                sink.Update();
            }


            if (!m_ControlsCreated)
            {
                CreateTermControls();
                m_ControlsCreated = true;
            }

            sink.CurrentInputChanged -= OnPowerInputChanged;
            sink.CurrentInputChanged += OnPowerInputChanged;

            if (m_block?.CubeGrid?.Physics != null)
            {

                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;

                LastRunTick = MyAPIGateway.Session.GameplayFrameCounter;

            }

            m_block.SetEmissivePartsForSubparts("ShinyOrb",new Color(16,0,91), 0.65f);

            base.UpdateOnceBeforeFrame();

        }


        public override void UpdateAfterSimulation100()
        {

            int tick = MyAPIGateway.Session.GameplayFrameCounter;
            float dt = (tick - LastRunTick) / MyEngineConstants.UPDATE_STEPS_PER_SECOND;
            LastRunTick = tick;

            if (IsWorking && MyAPIGateway.Multiplayer.IsServer)
            {
                itemTotal = 0;
                m_Inv.Clear();

                m_block.GetInventory().GetItems(m_Inv);//[Func<MyInventoryItem, bool>isItem]
                                                       // MyLog.Default.WriteLine("Len.Inventory Slots : " + m_Inv.Count);
                foreach (var slot in m_Inv)
                {
                    itemTotal += MyFixedPoint.AddSafe(itemTotal, slot.Amount);
                }
                //MyLog.Default.WriteLine("Len.Total Items : " + itemTotal);
                //MyLog.Default.WriteLine("Len.ToPrint : " + m_toMake);
                if ((itemTotal >= mess_toMake.Value && mess_toMake.Value != 0) || mess_SelectedAmmo.Value == 0)
                {
                    //MyLog.Default.WriteLine("Len.Thing full or broke");
                    UpdateDetailInfo();
                    //m_block.RefreshCustomInfo();
                    return;
                }


                //MyLog.Default.WriteLine("Len.LoadingAmmoID");
                MyDefinitionId AmmoBpId = m_AmmoIdSlot[mess_SelectedAmmo.Value];
                //MyLog.Default.WriteLine("Len.LoadingTotalKW");
                float TotalKW = m_AvailAmmo[AmmoBpId];
                Item AmmoId = m_BPtoAmmo[AmmoBpId];



                //MyLog.Default.WriteLine("Len.Block Power built: " + builtPower);
                if (mess_BuiltPower.Value >= TotalKW)
                {
                    //MyLog.Default.WriteLine("Len.Trying to make " + AmmoBpId);
                    mess_BuiltPower.ValidateAndSet(0f);
                    MyObjectBuilder_PhysicalObject theAmmo = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(AmmoId.Id);
                    m_block.GetInventory().AddItems(1, theAmmo);
                }
                //builtPower += RequiredOperationalPower * dt;
                mess_BuiltPower.ValidateAndSet(mess_BuiltPower.Value + (RequiredOperationalPower * dt));

                itemTotal = 0;
                m_Inv.Clear();
            }
            UpdateDetailInfo();
            //m_block.RefreshCustomInfo();
        }
        public override void Init(MyObjectBuilder_EntityBase objBuilder)
        {

            //MyLog.Default.WriteLine("Len.Initing A2E");
            m_block = Entity as IMyConveyorSorter;


            m_OperationPower = 1f * m_PowerMulti;
            m_StandbyPower = 0.01f;

            m_block.AppendingCustomInfo += OnAppendingCustomInfo;

            InitAmmoAvail();
            /*
            lock (m_customControls)
            {
                if (m_customControls.Count == 0)
                {
                    CreateTermControls();
                }
            }
            */
            


            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            MyLog.Default.WriteLine("Len.Init Success!");
        }

        public void LoadState()
        {
            E2AState state = null;
            var ent = m_block;
            string data;
            if (ent.Storage == null) { ent.Storage = new MyModStorageComponent(); }
            if (!ent.Storage.TryGetValue(ModStorageID, out data)) 
            { 
                MyLog.Default.WriteLine("Len.NoStorage!");
            }

            if (ent.Storage != null && ent.Storage.TryGetValue(ModStorageID, out data))
            {
                try
                {
                    state = MyAPIGateway.Utilities.SerializeFromXML<E2AState>(data);
                }
                catch (Exception ex)
                {
                    MyLog.Default.WriteLine("Len.ERROR in LOADSTATE: " + ex);
                }
                //MyLog.Default.WriteLine("Len.Loadstate Loaded.");
            }

            if (state == null)
            {
                state = new E2AState();
                //MyLog.Default.WriteLine("Len.Loadstate Null!");
            }


            mess_toMake.SetLocalValue((int)MathHelper.Max(state.ToMake, 0));
            mess_SelectedAmmo.SetLocalValue((int)MathHelper.Max(m_AmmoIdSlot.IndexOf(state.SelectedAmmo), 0));
            mess_SpeedMulti.SetLocalValue(MathHelper.Clamp(state.SpeedMulti, .5f, 5f));
            mess_BuiltPower.ValidateAndSet(MathHelper.Max(state.powerBuilt, 0f));

            m_block.RefreshCustomInfo();
        }

        public void OnPowerInputChanged(MyDefinitionId resourceTypeId, float oldInput, MyResourceSinkComponent sink)
        {
            if (resourceTypeId != MyResourceDistributorComponent.ElectricityId || sink != m_block.ResourceSink)
                return;

            var currentInput = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
            m_isPowered = currentInput >= m_lastRequiredInput;
            sink.Update();
        }

        public override void UpdatingStopped()
        {
            if (MyAPIGateway.Multiplayer.IsServer) { IsSerialized(); }
            base.UpdatingStopped();
        }

        /*
        private void OnBlockRemoved(VRage.Game.ModAPI.IMySlimBlock block)
        {
            var fat = block.FatBlock;
            if (fat == null) return;
            if (fat == m_block) { ((MyCubeBlock)m_block).ReleaseInventory((MyInventory)m_block.GetInventory()); }
        }
      */

        public void InitAmmoAvail()
        {
            //MyLog.Default.WriteLine("Len.Init Ammo Avail");
            if (m_AvailAmmo.Count == 0)
            {
                lock (m_AvailAmmo)
                {
                    //MyLog.Default.WriteLine("Len.Locked Ammo Init");
                    HashSet<MyBlueprintDefinitionBase> BPSet = new HashSet<MyBlueprintDefinitionBase>();


                    foreach (MyDefinitionBase Base in MyDefinitionManager.Static.GetAllDefinitions())
                    {
                        var anger = Base as MyProductionBlockDefinition;
                        if (anger != null)
                        {
                            if (!(anger is MyGasTankDefinition || anger is MyOxygenGeneratorDefinition))
                            {
                                foreach (MyBlueprintClassDefinition bpClass in anger.BlueprintClasses)
                                {
                                    foreach (MyBlueprintDefinitionBase BP in bpClass)
                                    {
                                        if (!(BP.Results.Length == 1 && BP.Results[0].Id.TypeId == typeof(MyObjectBuilder_AmmoMagazine))) { continue; }
                                        BPSet.Add(BP);
                                    }
                                }
                            }
                        }
                    }
                    MyLog.Default.WriteLine("Len.Writing Costs");
                    //MyLog.Default.WriteLine("Len.By the way, powerMulti is " + m_PowerMulti);
                    foreach (MyBlueprintDefinitionBase bp in BPSet)
                    {
                        if (!(bp.AvailableInSurvival)) continue;
                        Item[] AmmoMats = bp.Prerequisites;
                        MyDefinitionId AmmoID = bp.Id;
                        Item[] ammoGiven = bp.Results;
                        MyFixedPoint totalKW = 0;
                        foreach (Item material in AmmoMats)
                        {
                            string matsubid = material.Id.SubtypeName;
                            MyFixedPoint amount = material.Amount;
                            //MyLog.Default.WriteLine("Len.Looking for material " + matsubid + " for " + material + " of amount " + amount);
                            switch (matsubid)
                            {
                                case "Iron":
                                    totalKW += MyFixedPoint.MultiplySafe(7.5f, amount);
                                    break;
                                case "Nickel":
                                    totalKW += MyFixedPoint.MultiplySafe(9f, amount);
                                    break;
                                case "Cobalt":
                                    totalKW += MyFixedPoint.MultiplySafe(15f, amount);
                                    break;
                                case "Magnesium":
                                    totalKW += MyFixedPoint.MultiplySafe(23.5f, amount);
                                    break;
                                case "Silicon":
                                    totalKW += MyFixedPoint.MultiplySafe(5f, amount);
                                    break;
                                case "Silver":
                                    totalKW += MyFixedPoint.MultiplySafe(15f, amount);
                                    break;
                                case "Gold":
                                    totalKW += MyFixedPoint.MultiplySafe(20f, amount);
                                    break;
                                case "Uranium":
                                    totalKW += MyFixedPoint.MultiplySafe(60f, amount);
                                    break;
                                case "Platinum":
                                    totalKW += MyFixedPoint.MultiplySafe(30f, amount);
                                    break;
                                default:
                                    totalKW += 2000;
                                    break;

                            }

                        }
                        //MyFixedPoint finalKW = (MyFixedPoint.MultiplySafe(totalKW, m_PowerMulti))/2;
                        float finalKW = (float)totalKW * m_PowerMulti;
                        MyLog.Default.WriteLine("Len.Cost of " + AmmoID + " Is " + finalKW);
                        m_AvailAmmo.Add(AmmoID, finalKW);
                        m_BPtoAmmo.Add(AmmoID, ammoGiven[0]);
                        m_AmmoIdSlot.Add(AmmoID);
                        m_AmmoNames.Add(bp.DisplayNameText);
                    }
                    m_AvailAmmo.Add(MyDefinitionId.Parse("MyObjectBuilder_Ingot/Scrap"), 0);
                    m_AmmoIdSlot.Insert(0, MyDefinitionId.Parse("MyObjectBuilder_Ingot/Scrap"));
                    m_AmmoNames.Insert(0, "Old Scrap Metal");
                }
            }
            MyLog.Default.WriteLine("Len.AmmoList contains: " + m_AvailAmmo.Count + " Entries");
        }

        public static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (block == null || block.GameLogic == null)
                return;

            var logic = block.GameLogic.GetAs<E2ALogic>();

            if (logic != null)
            {

                for (int i = (controls.Count - 1); i >= 0; i--)
                {
                    IMyTerminalControl control = controls[i];

                    switch (control.Id)
                    {
                        case "DrainAll":
                            controls.RemoveAt(i);
                            break;
                        case "blacklistWhitelist":
                            controls.RemoveAt(i);
                            break;
                        case "CurrentList":
                            controls.RemoveAt(i);
                            break;
                        case "removeFromSelectionButton":
                            controls.RemoveAt(i);
                            break;
                        case "candidatesList":
                            controls.RemoveAt(i);
                            break;
                        case "addToSelectionButton":
                            controls.RemoveAt(i);
                            break;
                        default:
                            break;
                    }
                }
                //if(!m_ControlsCreated) { CreateTermControls();  m_ControlsCreated = true; }

                controls.AddRange(m_customControls);
                /*
                if ((VRage.ModAPI.IMyEntity)block == logic.Entity && !logic.IsTerminalOpen)
                {
                    logic.IsTerminalOpen = true;
                    logic.UpdateDetailInfo();
                }
                */
            }
        }

        private void OnAppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {


            sb.AppendLine("Required Power : ");
            MyValueFormatter.AppendWorkInBestUnit(PowerReq(), sb);

            sb.AppendLine();
            sb.AppendLine();
            sb.Append("Status : ");

            if (!block.IsWorking || mess_SelectedAmmo.Value == 0) { sb.AppendLine("Offline"); }
            if (m_isPowered)
            {
                if (mess_SelectedAmmo.Value != 0)
                {
                    sb.AppendLine("Online - Creating : " + m_AmmoNames[mess_SelectedAmmo.Value]);
                    sb.AppendLine("Speed Multiplier : " + mess_SpeedMulti.Value);
                    sb.AppendLine("Progress : " + MathHelper.Clamp(100 * (mess_BuiltPower.Value / (float)(m_AvailAmmo[m_AmmoIdSlot[mess_SelectedAmmo.Value]] * requiredPowerMulti)), 0, 100) + "%");
                }
                else
                {
                    sb.AppendLine("Idle");
                }
            }
            else
            {
                sb.AppendLine("Paused");
                if (m_isPowered) { sb.AppendLine("\nInventory Full!"); }
                else
                {
                    sb.AppendLine("\nNot enough power!");
                    sb.AppendLine("Required operating power : ");
                    MyValueFormatter.AppendWorkInBestUnit(RequiredOperationalPower, sb);
                }
            }
        }

        public static void CreateTermControls()
        {

            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;

            CreateAmmoSelect();
            CreateCountSelect();
            CreateSpeedSelect();

        }

        private static void CreateAmmoSelect()
        {

            var dropdown = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyConveyorSorter>("E2AComboBoxAmmo");

            dropdown.Title = MyStringId.GetOrCompute("Ammo To Print");
            dropdown.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                {
                    return 0;
                }
                var logic = block.GameLogic.GetAs<E2ALogic>();
                return logic != null ? logic.mess_SelectedAmmo.Value : 0;
            };
            dropdown.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();

                if (logic != null)
                {
                    logic.mess_SelectedAmmo.ValidateAndSet(MathHelper.Floor(value));
                }
                logic.IsSerialized();
            };

            dropdown.ComboBoxContent = (ele) =>
            {
                ele.Clear();
                foreach (string ammo in m_AmmoNames)
                {
                    MyStringId s = MyStringId.GetOrCompute(ammo);
                    if (s == MyStringId.GetOrCompute("Old Scrap Metal"))
                    {
                        s = MyStringId.GetOrCompute("Idle");
                    }
                    ele.Add(new MyTerminalControlComboBoxItem { Key = ele.Count, Value = s });
                }
            };

            m_customControls.Add(dropdown);
        }


        private static void CreateCountSelect()
        {
            var countsel = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyConveyorSorter>("E2ASliderCount");

            countsel.Title = MyStringId.GetOrCompute("Max In Inventory");
            countsel.SetLimits(0, 30);
            countsel.Tooltip = MyStringId.GetOrCompute("Max Items In Inventory. 0 == Unlimited.");
            countsel.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                {
                    return 0;
                }
                var logic = block.GameLogic.GetAs<E2ALogic>();
                return logic != null ? logic.mess_toMake.Value : 0;
            };
            countsel.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();
                if (logic != null)
                    logic.mess_toMake.ValidateAndSet(MathHelper.Floor(value)) ;

                logic.IsSerialized();
            };
            countsel.Writer = (block, sb) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();
                if (logic != null)
                {
                    MyValueFormatter.AppendGenericInBestUnit(logic.mess_toMake.Value, sb);
                }
            };
            m_customControls.Add(countsel);
        }

        private static void CreateSpeedSelect()
        {
            var countsel = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyConveyorSorter>("E2ASliderSpeed");

            countsel.Title = MyStringId.GetOrCompute("Speed Multiplier");
            countsel.SetLimits(0.5f, 5f);
            countsel.Tooltip = MyStringId.GetOrCompute("TotalPowerPerAmmo=BasePower * (1.25 * SpeedMultiplier)");
            countsel.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                {
                    return 1f;
                }
                var logic = block.GameLogic.GetAs<E2ALogic>();
                return logic != null ? logic.mess_SpeedMulti.Value : 1f;
            };
            countsel.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();
                if (logic != null)
                    logic.mess_SpeedMulti.ValidateAndSet(MathHelper.Floor(value));
                logic.IsSerialized();
            };
            countsel.Writer = (block, sb) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();
                if (logic != null)
                {
                    MyValueFormatter.AppendGenericInBestUnit(logic.mess_SpeedMulti.Value, sb);
                }
            };
            m_customControls.Add(countsel);



        }

        protected void OnMessage_AmmoChanged(MySync<int, SyncDirection.BothWays> obj)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                //MyAPIGateway.Utilities.SendMessage("OnMessage_AmmoChanged");
                IsSerialized();
            }

        }


        protected void OnMessage_SpeedChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                //MyAPIGateway.Utilities.SendMessage("OnMessage_SpeedChanged");
                IsSerialized();
            }

        }


        protected void OnMessage_MakeChanged(MySync<int, SyncDirection.BothWays> obj)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                //MyAPIGateway.Utilities.SendMessage("OnMessage_MakeChanged");
                IsSerialized();
            }

        }



        public void UpdateDetailInfo()
        {
            m_block.RefreshCustomInfo();
            //MyLog.Default.WriteLine("Len.RefreshedInfo");
            /*
            if (m_block !=null && MyAPIGateway.Gui.InteractedEntity == m_block)
            {
                //MyLog.Default.WriteLine("Len.Didthestupid");
                var action = m_block.GetActionWithName("ShowInToolbarConfig");
                action?.Apply(m_block);
                action?.Apply(m_block);
            }
            */
        }

        public void SaveState()
        {
                MyLog.Default.WriteLine("Len.StartSave");
                if (!MyAPIGateway.Multiplayer.IsServer) return;

            var state = new E2AState()
            {
                Version = 1,
                SelectedAmmo = m_AmmoIdSlot[mess_SelectedAmmo.Value],
                ToMake = mess_toMake.Value,
                SpeedMulti = mess_SpeedMulti.Value,
                powerBuilt = mess_BuiltPower.Value
                };

                MyLog.Default.WriteLine("Selected ammo is " + state.SelectedAmmo);
                if (Entity.Storage == null) { Entity.Storage = new MyModStorageComponent(); }
                Entity.Storage[ModStorageID] = MyAPIGateway.Utilities.SerializeToXML(state);
                MyLog.Default.WriteLine("Len.FinishSave");
        }


        public override bool IsSerialized()
        {
            try
            {
                SaveState();
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("Len.Error" + e);
            }

            return base.IsSerialized();
        }


        private const string SUBPART_NAME = "subpart_ORB";
        private bool FirstFind = true;
        private Matrix subpartLocalMatrix;
        private readonly Vector3 DIR_UP = Vector3.Up;
        private bool Ponder;


        double Floaty(double amplitude, double oscilationsPerSecond, double timeSeconds)
        {
            return amplitude * Math.Sin(2 * Math.PI * oscilationsPerSecond * timeSeconds);
        }

        private void Float()
        {
            try
            {

                MyEntitySubpart ORB;
                Entity.TryGetSubpart(SUBPART_NAME, out ORB);
                if (m_isPowered)
                {


                    if (ORB.Render.IsVisible() && !IsWorking)
                    {
                        ORB.Render.Visible = false;
                    } else
                    {
                        ORB.Render.Visible = true;
                    }
                    

                    var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
                    if (Vector3D.DistanceSquared(camPos,m_block.GetPosition()) > (250*250))
                    {
                        return;
                    }

                    

                    if (Ponder)
                    {
                        if (FirstFind)
                        {
                            FirstFind = false;
                            subpartLocalMatrix = ORB.PositionComp.LocalMatrixRef;
                        }
                    }
                    Matrix local = subpartLocalMatrix;
                    local.Translation += local.Up * (float)Floaty(0.25,0.5,MyAPIGateway.Session.GameplayFrameCounter);
                    ORB.PositionComp.SetLocalMatrix(ref local);
                    subpartLocalMatrix = Matrix.Normalize(subpartLocalMatrix);
                    
                }
                else
                {
                    ORB.Render.Visible = false;
                }
            } catch (Exception e)
            {
                MyLog.Default.WriteLine("Len.ERORR: " + e);
            }

        }

    }
}

