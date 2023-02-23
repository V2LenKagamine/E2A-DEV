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
using Sandbox.Game.Lights;
using IMyInventory = VRage.Game.ModAPI.Ingame.IMyInventory;
using Sandbox.Game.GameSystems;
using Sandbox.Game.GameSystems.Conveyors;

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

        private static float powerRequiredBase = 0.05f;

        List<MyInventoryItem> m_Inv = new List<MyInventoryItem>();
        MyFixedPoint itemTotal;
        MyFixedPoint itemTotal2;
        MyFixedPoint itemTotal3;

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
        public MySync<float, SyncDirection.FromServer> mess_BuiltPower2 = null;
        public MySync<float, SyncDirection.FromServer> mess_BuiltPower3 = null;
        public MySync<float, SyncDirection.BothWays> mess_SpeedMulti = null;
        public MySync<int, SyncDirection.BothWays> mess_toMake = null;
        public MySync<int, SyncDirection.BothWays> mess_toMake2 = null;
        public MySync<int, SyncDirection.BothWays> mess_toMake3 = null;
        public MySync<int, SyncDirection.BothWays> mess_SelectedAmmo = null;
        public MySync<int, SyncDirection.BothWays> mess_SelectedAmmo2 = null;
        public MySync<int, SyncDirection.BothWays> mess_SelectedAmmo3 = null;

        private static Color orbColor = new Color(33, 222, 242);
        private static Vector4D orbVecColor = orbColor.ToVector4();

        private int LastRunTick = -1;


        public float requiredPowerMulti
        {
            get { return m_PowerMulti * BlockSizePowerMulti; }
        }

        private bool IsWorking
        {
            get 
            { 
                if (m_block.CubeGrid.GridSizeEnum == 0)
                {
                    return m_block.Enabled && m_block.IsFunctional && (mess_SelectedAmmo.Value != 0 || mess_SelectedAmmo2.Value != 0 || mess_SelectedAmmo3.Value != 0);
                }
                else
                {
                    return m_block.Enabled && m_block.IsFunctional && mess_SelectedAmmo.Value != 0;
                }
            }
        }

        private int m_OpsRunning
        {
            get
            {
                int running = 0;
                if (mess_SelectedAmmo.Value != 0) { running++; }
                if (mess_SelectedAmmo2.Value != 0) { running++; }
                if (mess_SelectedAmmo3.Value != 0) { running++; }

                return running;
            }
        }

        private float RequiredOperationalPower
        {
            get 
            {
                if (m_block.CubeGrid.GridSizeEnum == 0)
                {
                    

                    return m_OpsRunning * m_OperationPower * m_PowerMulti * mess_SpeedMulti.Value * BlockSizeSpeedMulti;
                }
                else
                {
                    return m_OperationPower * m_PowerMulti * mess_SpeedMulti.Value * BlockSizeSpeedMulti;
                }
            }

        }
        private float BlockSizePowerMulti
        {
            get
            {
                if (m_block.CubeGrid.GridSizeEnum == 0)
                {
                    return 0.90f;
                }
                else
                {
                    return 1f;
                }
            }
        }
        private float BlockSizeSpeedMulti
        {
            get
            {
                if (m_block.CubeGrid.GridSizeEnum == 0)
                {
                    return 3f;
                }
                else
                {
                    return 1f;
                }
            }
        }
        public override void Close()
        {
            foreach  (var l in Lightlist.Values) { MyLights.RemoveLight(l); }
            ((MyResourceSinkComponent)m_block.ResourceSink).CurrentInputChanged -= OnPowerInputChanged;
            found = false;
            FirstFind = true;
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

            if (m_block?.CubeGrid?.Physics == null) { return; }

            var sink = Entity.Components.Get<MyResourceSinkComponent>();
            //MyLog.Default.WriteLine("Len.Loading Last State");
            LoadState();
            SaveState();

            //mess_SelectedAmmo = null;
            //MyLog.Default.WriteLine("Len.AmmoChanged = " + mess_SelectedAmmo);
            mess_SelectedAmmo.ValueChanged += OnMessage_AmmoChanged;
            mess_SelectedAmmo2.ValueChanged += OnMessage_AmmoChanged;
            mess_SelectedAmmo3.ValueChanged += OnMessage_AmmoChanged;

            //mess_SpeedMulti = null;
            //MyLog.Default.WriteLine("Len.SpeedChanged = " + mess_SpeedMulti);
            mess_SpeedMulti.ValueChanged += OnMessage_SpeedChanged;

            //mess_SpeedMulti = null;
            //MyLog.Default.WriteLine("Len.MakeChanged = " + mess_toMake);
            mess_toMake.ValueChanged += OnMessage_MakeChanged;
            mess_toMake2.ValueChanged += OnMessage_MakeChanged;
            mess_toMake3.ValueChanged += OnMessage_MakeChanged;

            if (sink != null)
            {
                sink.SetRequiredInputFuncByType(MyResourceDistributorComponent.ElectricityId, PowerReq);
                sink.Update();
            }


            if (!m_ControlsCreated)
            {
                CreateBigTermControls();
                m_ControlsCreated = true;
            }

            sink.CurrentInputChanged -= OnPowerInputChanged;
            sink.CurrentInputChanged += OnPowerInputChanged;

            if (m_block?.CubeGrid?.Physics != null)
            {

                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;

                LastRunTick = MyAPIGateway.Session.GameplayFrameCounter;

            }
            base.UpdateOnceBeforeFrame();
            if (MyAPIGateway.Utilities.IsDedicated) { return; }
            m_block.SetEmissivePartsForSubparts("EmissiveColorable",orbColor,0.75f);
        }


        public override void UpdateAfterSimulation100()
        {

            int tick = MyAPIGateway.Session.GameplayFrameCounter;
            float dt = (tick - LastRunTick) / MyEngineConstants.UPDATE_STEPS_PER_SECOND;
            LastRunTick = tick;

            if (IsWorking && MyAPIGateway.Multiplayer.IsServer)
            {

                if (m_block.CubeGrid.GridSizeEnum == 0)
                {
                    /*
                     * Good god this is a mess, clean this up later maybe.
                     */
                    itemTotal = 0;
                    itemTotal2 = 0;
                    itemTotal3 = 0;
                    m_Inv.Clear();
                    float TotalKW = 0;
                    float TotalKW2 = 0;
                    float TotalKW3 = 0;
                    MyDefinitionId AmmoBpId;
                    MyDefinitionId AmmoBpId2;
                    MyDefinitionId AmmoBpId3;
                    bool canfitammo1 = false;
                    bool canfitammo2 = false;
                    bool canfitammo3 = false;
                    MyDefinitionId.TryParse("MyObjectBuilder_ConsumableItem/ClangCola", out AmmoBpId);
                    MyDefinitionId.TryParse("MyObjectBuilder_ConsumableItem/ClangCola", out AmmoBpId2);
                    MyDefinitionId.TryParse("MyObjectBuilder_ConsumableItem/ClangCola", out AmmoBpId3);
                    //MyLog.Default.WriteLine("Len.LoadingAmmoID");
                    if (mess_SelectedAmmo != 0) { AmmoBpId = m_AmmoIdSlot[mess_SelectedAmmo.Value]; }
                    if (mess_SelectedAmmo2 != 0) { AmmoBpId2 = m_AmmoIdSlot[mess_SelectedAmmo2.Value]; }
                    if (mess_SelectedAmmo3 != 0) { AmmoBpId3 = m_AmmoIdSlot[mess_SelectedAmmo3.Value]; }
                    //MyLog.Default.WriteLine("Len.LoadingTotalKW");
                     

                    m_block.GetInventory().GetItems(m_Inv);
                     //MyLog.Default.WriteLine("Len.Inventory Slots : " + m_Inv.Count);
                    foreach (var slot in m_Inv)
                    {
                        if (AmmoBpId.SubtypeName != "ClangCola") {
                            if (slot.Type.SubtypeId == MyDefinitionManager.Static.GetBlueprintDefinition(AmmoBpId).Results[0].Id.SubtypeName)
                            {
                                itemTotal += MyFixedPoint.AddSafe(itemTotal, slot.Amount);
                            }
                        }
                        if (AmmoBpId2.SubtypeName != "ClangCola")
                        {
                            if (slot.Type.SubtypeId == MyDefinitionManager.Static.GetBlueprintDefinition(AmmoBpId2).Results[0].Id.SubtypeName)
                            {
                                itemTotal2 += MyFixedPoint.AddSafe(itemTotal2, slot.Amount);
                            }
                        }
                        if (AmmoBpId3.SubtypeName != "ClangCola")
                        {
                            if (slot.Type.SubtypeId == MyDefinitionManager.Static.GetBlueprintDefinition(AmmoBpId3).Results[0].Id.SubtypeName)
                            {
                                itemTotal3 += MyFixedPoint.AddSafe(itemTotal3, slot.Amount);
                            }
                        }
                    }
                    if ((itemTotal <= mess_toMake.Value || mess_toMake.Value == 0) && mess_SelectedAmmo.Value != 0)
                    {
                        TotalKW = m_AvailAmmo[AmmoBpId] * requiredPowerMulti;
                        int ttm = (int)Math.Floor(mess_BuiltPower.Value / TotalKW);
                        canfitammo1 = m_block.GetInventory().CanItemsBeAdded(ttm, MyDefinitionManager.Static.GetBlueprintDefinition(AmmoBpId).Results[0].Id);
                        if (canfitammo1)
                        {
                            //MyLog.Default.WriteLine("Len.Block Power built: " + builtPower);
                            if (mess_BuiltPower.Value >= TotalKW)
                            {
                                Item AmmoId = m_BPtoAmmo[AmmoBpId];
                                MyObjectBuilder_PhysicalObject theAmmo = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(AmmoId.Id);
                                //MyLog.Default.WriteLine("Len.Trying to make " + AmmoBpId);
                                mess_BuiltPower.ValidateAndSet(mess_BuiltPower.Value - (TotalKW * ttm));

                                m_block.GetInventory().AddItems(MathHelper.Clamp(ttm, 1, int.MaxValue), theAmmo);
                            }
                            mess_BuiltPower.ValidateAndSet(mess_BuiltPower.Value + ((RequiredOperationalPower * mess_SpeedMulti.Value) / m_OpsRunning * dt));
                        }
                    }
                    if ((itemTotal2 <= mess_toMake2.Value || mess_toMake2.Value == 0) && mess_SelectedAmmo2.Value != 0)
                    {
                        TotalKW2 = m_AvailAmmo[AmmoBpId2] * requiredPowerMulti;
                        int ttm = (int)Math.Floor(mess_BuiltPower2.Value / TotalKW2);
                        canfitammo2 = m_block.GetInventory().CanItemsBeAdded(ttm, MyDefinitionManager.Static.GetBlueprintDefinition(AmmoBpId2).Results[0].Id);
                        if (canfitammo2)
                        {
                            //MyLog.Default.WriteLine("Len.Block Power built: " + builtPower);
                            if (mess_BuiltPower2.Value >= TotalKW2)
                            {
                                Item AmmoId2 = m_BPtoAmmo[AmmoBpId2];
                                MyObjectBuilder_PhysicalObject theAmmo = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(AmmoId2.Id);
                                //MyLog.Default.WriteLine("Len.Trying to make " + AmmoBpId);
                                mess_BuiltPower2.ValidateAndSet(mess_BuiltPower2.Value - (TotalKW2 * ttm));

                                m_block.GetInventory().AddItems(MathHelper.Clamp(ttm, 1, int.MaxValue), theAmmo);
                            }
                            mess_BuiltPower2.ValidateAndSet(mess_BuiltPower2.Value + ((RequiredOperationalPower * mess_SpeedMulti.Value) / m_OpsRunning * dt));
                        }
                    }
                    if ((itemTotal3 <= mess_toMake3.Value || mess_toMake3.Value == 0) && mess_SelectedAmmo3.Value != 0)
                    {
                        TotalKW3 = m_AvailAmmo[AmmoBpId3] * requiredPowerMulti;
                        int ttm = (int)Math.Floor(mess_BuiltPower3.Value / TotalKW3);
                        canfitammo3 = m_block.GetInventory().CanItemsBeAdded(ttm, MyDefinitionManager.Static.GetBlueprintDefinition(AmmoBpId3).Results[0].Id);
                        if (canfitammo3)
                        {
                            //MyLog.Default.WriteLine("Len.Block Power built: " + builtPower);
                            if (mess_BuiltPower3.Value >= TotalKW3)
                            {
                                Item AmmoId3 = m_BPtoAmmo[AmmoBpId3];
                                MyObjectBuilder_PhysicalObject theAmmo = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(AmmoId3.Id);
                                //MyLog.Default.WriteLine("Len.Trying to make " + AmmoBpId);
                                mess_BuiltPower3.ValidateAndSet(mess_BuiltPower3.Value - (TotalKW3 * ttm));

                                m_block.GetInventory().AddItems(MathHelper.Clamp(ttm,1,int.MaxValue), theAmmo);
                            }
                            mess_BuiltPower3.ValidateAndSet(mess_BuiltPower3.Value + ((RequiredOperationalPower * mess_SpeedMulti.Value) / m_OpsRunning * dt));
                        }
                    }
              
                    itemTotal = 0;
                    itemTotal2 = 0;
                    itemTotal3 = 0;
                    m_Inv.Clear();
                    UpdateDetailInfo();
                }
                else
                {

                    itemTotal = 0;
                    m_Inv.Clear();

                    m_block.GetInventory().GetItems(m_Inv);
                    //[Func<MyInventoryItem, bool>isItem]
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
                    float TotalKW = m_AvailAmmo[AmmoBpId] * requiredPowerMulti;
                    Item AmmoId = m_BPtoAmmo[AmmoBpId];

                    if (m_block.GetInventory().CanItemsBeAdded(1, MyDefinitionManager.Static.GetBlueprintDefinition(AmmoBpId).Results[0].Id))
                    {
                        //MyLog.Default.WriteLine("Len.Block Power built: " + builtPower);
                        if (mess_BuiltPower.Value >= TotalKW)
                        {
                            MyObjectBuilder_PhysicalObject theAmmo = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(AmmoId.Id);
                            //MyLog.Default.WriteLine("Len.Trying to make " + AmmoBpId);
                            mess_BuiltPower.ValidateAndSet(mess_BuiltPower.Value - TotalKW);

                            m_block.GetInventory().AddItems(1, theAmmo);
                        }
                    }
                    //builtPower += RequiredOperationalPower * dt;
                    mess_BuiltPower.ValidateAndSet(mess_BuiltPower.Value + (RequiredOperationalPower * dt));

                    itemTotal = 0;
                    m_Inv.Clear();
                }
            }
            UpdateDetailInfo();
            //m_block.RefreshCustomInfo();
        }

        public override void Init(MyObjectBuilder_EntityBase objBuilder)
        {

            //MyLog.Default.WriteLine("Len.Initing A2E");
            m_block = Entity as IMyConveyorSorter;


            m_OperationPower = powerRequiredBase * m_PowerMulti;
            m_StandbyPower = 0.000001f;

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
            //MyLog.Default.WriteLine("Len.Init Success!");
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
            //Pretty sure loading an invalid ammo means crash. So try/catch here.
            try 
            { 
                mess_SelectedAmmo.SetLocalValue((int)MathHelper.Max(m_AmmoIdSlot.IndexOf(state.SelectedAmmo), 0));
                mess_SelectedAmmo2.SetLocalValue((int)MathHelper.Max(m_AmmoIdSlot.IndexOf(state.SelectedAmmo2), 0));
                mess_SelectedAmmo3.SetLocalValue((int)MathHelper.Max(m_AmmoIdSlot.IndexOf(state.SelectedAmmo3), 0));
            }
            catch (Exception e) 
            {
                MyLog.Default.WriteLine("Len.Error: " + e + "If you're seeing this in log, It means I was right and loading an invalid ammo causes an invalid index.");
                mess_SelectedAmmo.ValidateAndSet(0);
                mess_SelectedAmmo2.ValidateAndSet(0);
                mess_SelectedAmmo3.ValidateAndSet(0);
            }
            
            mess_SpeedMulti.SetLocalValue(MathHelper.Clamp(state.SpeedMulti, 1f, 5f));
            mess_BuiltPower.ValidateAndSet(MathHelper.Max(state.powerBuilt, 0f));
            mess_BuiltPower2.ValidateAndSet(MathHelper.Max(state.powerBuilt2, 0f));
            mess_BuiltPower3.ValidateAndSet(MathHelper.Max(state.powerBuilt3, 0f));

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
                                    totalKW += 200;
                                    break;

                            }

                        }
                        //MyFixedPoint finalKW = (MyFixedPoint.MultiplySafe(totalKW, m_PowerMulti))/2;
                        float finalGW = ((float)totalKW * m_PowerMulti) / 100;
                        MyLog.Default.WriteLine("Len.Cost of " + AmmoID + " Is " + finalGW + "GW");
                        m_AvailAmmo.Add(AmmoID, finalGW);
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
            if(block.CubeGrid.GridSizeEnum != 0) 
            {
                sb.AppendLine();
                sb.AppendLine("Ammo types #2 & #3 Disabled on small blocks!");
            }
            
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("Status : ");
            if (block.CubeGrid.GridSizeEnum == 0)
            {
                if (!block.IsWorking || (mess_SelectedAmmo.Value == 0 && mess_SelectedAmmo2.Value == 0 && mess_SelectedAmmo3.Value == 0)) { sb.AppendLine("Offline"); }
            }
            else
            {
                if (!block.IsWorking || mess_SelectedAmmo.Value == 0) { sb.AppendLine("Offline"); }
            }
            
            
            if (m_isPowered)
            {
                if (block.CubeGrid.GridSizeEnum == 0)
                {
                    if (mess_SelectedAmmo.Value != 0)
                    {
                        float ammoSelected = (float)m_AvailAmmo[m_AmmoIdSlot[mess_SelectedAmmo.Value]];
                        float totalCost = ammoSelected * requiredPowerMulti;
                        sb.AppendLine("Online - Creating : " + m_AmmoNames[mess_SelectedAmmo.Value]);
                        //sb.AppendLine("Speed Multiplier : " + mess_SpeedMulti.Value);
                        sb.AppendLine("Energy : " + mess_BuiltPower.Value + " / " + totalCost);
                        //sb.AppendLine("Progress : " + MathHelper.Clamp(100 * (mess_BuiltPower.Value / totalCost), 0, 100) + "%");
                        sb.AppendLine("Time Left : " + MathHelper.Floor(MathHelper.Clamp(Math.Ceiling((float)(totalCost / (powerRequiredBase * requiredPowerMulti)) - (mess_BuiltPower.Value / (powerRequiredBase * requiredPowerMulti))), 0, int.MaxValue) / BlockSizeSpeedMulti) + " Seconds.");
                    }
                    else
                    {
                        sb.AppendLine("Ammo 1 Idle");
                    }
                    if (mess_SelectedAmmo2.Value != 0)
                    {
                        float ammoSelected2 = (float)m_AvailAmmo[m_AmmoIdSlot[mess_SelectedAmmo2.Value]];
                        float totalCost2 = ammoSelected2 * requiredPowerMulti;
                        sb.AppendLine("Online - Creating : " + m_AmmoNames[mess_SelectedAmmo2.Value]);
                        //sb.AppendLine("Speed Multiplier : " + mess_SpeedMulti.Value);
                        sb.AppendLine("Energy : " + mess_BuiltPower2.Value + " / " + totalCost2);
                        //sb.AppendLine("Progress : " + MathHelper.Clamp(100 * (mess_BuiltPower2.Value / totalCost2), 0, 100) + "%");
                        sb.AppendLine("Time Left : " + MathHelper.Floor(MathHelper.Clamp(Math.Ceiling((float)(totalCost2 / (powerRequiredBase * requiredPowerMulti)) - (mess_BuiltPower2.Value / (powerRequiredBase * requiredPowerMulti))), 0, int.MaxValue) / BlockSizeSpeedMulti) + " Seconds.");
                    }
                    else
                    {
                        sb.AppendLine("Ammo 2 Idle");
                    }
                    if (mess_SelectedAmmo3.Value != 0)
                    {
                        float ammoSelected3 = (float)m_AvailAmmo[m_AmmoIdSlot[mess_SelectedAmmo3.Value]];
                        float totalCost3 = ammoSelected3 * requiredPowerMulti;
                        sb.AppendLine("Online - Creating : " + m_AmmoNames[mess_SelectedAmmo3.Value]);
                        //sb.AppendLine("Speed Multiplier : " + mess_SpeedMulti.Value);
                        sb.AppendLine("Energy : " + mess_BuiltPower3.Value + " / " + totalCost3);
                        //sb.AppendLine("Progress : " + MathHelper.Clamp(100 * (mess_BuiltPower3.Value / totalCost3), 0, 100) + "%");
                        sb.AppendLine("Time Left : " + MathHelper.Floor(MathHelper.Clamp(Math.Ceiling((float)(totalCost3 / (powerRequiredBase * requiredPowerMulti)) - (mess_BuiltPower3.Value / (powerRequiredBase * requiredPowerMulti))), 0, int.MaxValue)/BlockSizeSpeedMulti) + " Seconds.");
                    }
                    else
                    {
                        sb.AppendLine("Ammo 3 Idle");
                    }
                    sb.AppendLine("Speed Multiplier : " + mess_SpeedMulti.Value);
                } else
                {
                    if (mess_SelectedAmmo.Value != 0)
                    {
                        float ammoSelected = (float)m_AvailAmmo[m_AmmoIdSlot[mess_SelectedAmmo.Value]];
                        float totalCost = ammoSelected * requiredPowerMulti;
                        sb.AppendLine("Online - Creating : " + m_AmmoNames[mess_SelectedAmmo.Value]);
                        sb.AppendLine("Speed Multiplier : " + mess_SpeedMulti.Value);
                        sb.AppendLine("Progress : " + MathHelper.Clamp(100 * (mess_BuiltPower.Value / totalCost), 0, 100) + "%");
                        sb.AppendLine("Time Left : " + MathHelper.Floor(MathHelper.Clamp(Math.Ceiling((float)(totalCost / (powerRequiredBase * requiredPowerMulti)) - (mess_BuiltPower.Value / (powerRequiredBase * requiredPowerMulti))), 0, int.MaxValue) / BlockSizeSpeedMulti) + " Seconds.");
                    }
                    else
                    {
                        sb.AppendLine("Idle");
                    }
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


        public static void CreateBigTermControls()
        {

            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;

            CreateAmmoSelect();
            CreateCountSelect();

            CreateAmmo2Select();
            CreateCount2Select();

            CreateAmmo3Select();
            CreateCount3Select();

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

        private static void CreateAmmo2Select()
        {

            var dropdown = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyConveyorSorter>("E2AComboBoxAmmo2");

            dropdown.Title = MyStringId.GetOrCompute("Ammo To Print #2");
            dropdown.Tooltip = MyStringId.GetOrCompute("Does Nothing on Small Grid Blocks.");
            dropdown.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                {
                    return 0;
                }
                var logic = block.GameLogic.GetAs<E2ALogic>();
                return logic != null ? logic.mess_SelectedAmmo2.Value : 0;
            };
            dropdown.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();

                if (logic != null)
                {
                    logic.mess_SelectedAmmo2.ValidateAndSet(MathHelper.Floor(value));
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


        private static void CreateAmmo3Select()
        {

            var dropdown = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyConveyorSorter>("E2AComboBoxAmmo3");

            dropdown.Title = MyStringId.GetOrCompute("Ammo To Print #3");
            dropdown.Tooltip = MyStringId.GetOrCompute("Does Nothing on Small Grid Blocks.");
            dropdown.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                {
                    return 0;
                }
                var logic = block.GameLogic.GetAs<E2ALogic>();
                return logic != null ? logic.mess_SelectedAmmo3.Value : 0;
            };
            dropdown.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();

                if (logic != null)
                {
                    logic.mess_SelectedAmmo3.ValidateAndSet(MathHelper.Floor(value));
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
            countsel.SetLimits(0,250);
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

        private static void CreateCount2Select()
        {
            var countsel = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyConveyorSorter>("E2ASliderCount2");

            countsel.Title = MyStringId.GetOrCompute("Max In Inventory Ammo #2");
            countsel.SetLimits(0, 250);
            countsel.Tooltip = MyStringId.GetOrCompute("Max Items In Inventory. 0 == Unlimited.");
            countsel.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                {
                    return 0;
                }
                var logic = block.GameLogic.GetAs<E2ALogic>();
                return logic != null ? logic.mess_toMake2.Value : 0;
            };
            countsel.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();
                if (logic != null)
                    logic.mess_toMake2.ValidateAndSet(MathHelper.Floor(value));

            };
            countsel.Writer = (block, sb) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();
                if (logic != null)
                {
                    MyValueFormatter.AppendGenericInBestUnit(logic.mess_toMake2.Value, sb);
                }
            };
            m_customControls.Add(countsel);
        }



        private static void CreateCount3Select()
        {
            var countsel = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyConveyorSorter>("E2ASliderCount3");

            countsel.Title = MyStringId.GetOrCompute("Max In Inventory Ammo #3");
            countsel.SetLimits(0, 250);
            countsel.Tooltip = MyStringId.GetOrCompute("Max Items In Inventory. 0 == Unlimited.");
            countsel.Getter = (block) =>
            {
                if (block == null || block.GameLogic == null)
                {
                    return 0;
                }
                var logic = block.GameLogic.GetAs<E2ALogic>();
                return logic != null ? logic.mess_toMake3.Value : 0;
            };
            countsel.Setter = (block, value) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();
                if (logic != null)
                    logic.mess_toMake3.ValidateAndSet(MathHelper.Floor(value));

            };
            countsel.Writer = (block, sb) =>
            {
                var logic = block.GameLogic.GetAs<E2ALogic>();
                if (logic != null)
                {
                    MyValueFormatter.AppendGenericInBestUnit(logic.mess_toMake3.Value, sb);
                }
            };
            m_customControls.Add(countsel);
        }

        private static void CreateSpeedSelect()
        {
            var countsel = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyConveyorSorter>("E2ASliderSpeed");

            countsel.Title = MyStringId.GetOrCompute("Speed Multiplier");
            countsel.SetLimits(1f, 5f);
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
                UpdateDetailInfo();
                IsSerialized();
            }

        }


        protected void OnMessage_SpeedChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                UpdateDetailInfo();
                IsSerialized();
            }

        }


        protected void OnMessage_MakeChanged(MySync<int, SyncDirection.BothWays> obj)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                UpdateDetailInfo();
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
                //MyLog.Default.WriteLine("Len.StartSave");
                if (!MyAPIGateway.Multiplayer.IsServer) return;

            var state = new E2AState()
            {
                Version = 1,
                SelectedAmmo = m_AmmoIdSlot[mess_SelectedAmmo.Value],
                SelectedAmmo2 = m_AmmoIdSlot[mess_SelectedAmmo2.Value],
                SelectedAmmo3 = m_AmmoIdSlot[mess_SelectedAmmo3.Value],
                ToMake = mess_toMake.Value,
                ToMake2 = mess_toMake2.Value,
                ToMake3 = mess_toMake3.Value,
                SpeedMulti = mess_SpeedMulti.Value,
                powerBuilt = mess_BuiltPower.Value,
                powerBuilt2 = mess_BuiltPower2.Value,
                powerBuilt3 = mess_BuiltPower3.Value
            };

                //MyLog.Default.WriteLine("Selected ammo is " + state.SelectedAmmo);
                if (Entity.Storage == null) { Entity.Storage = new MyModStorageComponent(); }
                Entity.Storage[ModStorageID] = MyAPIGateway.Utilities.SerializeToXML(state);
                //MyLog.Default.WriteLine("Len.FinishSave");
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


        private Dictionary<string, MyLight> Lightlist = new Dictionary<string, MyLight>();

        void makeLight(IMyModelDummy ORB)
        {
            var thelight = MyLights.AddLight();
            thelight.Start("OrbLight");
            thelight.Color = orbColor;
            thelight.Range = m_block.CubeGrid.GridSize * 0.95f;
            thelight.Falloff = 1f;
            thelight.Intensity = 1000f;
            thelight.Position = ORB.Matrix.Translation;
            //thelight.ReflectorDirection = Vector3D.TransformNormal(Vector3D.TransformNormal(dummyMatrix.Forward, m_block.WorldMatrix), m_block.CubeGrid.WorldMatrixInvScaled);
            //thelight.ReflectorUp = Vector3D.TransformNormal(Vector3D.TransformNormal(dummyMatrix.Up, m_block.WorldMatrix), m_block.CubeGrid.WorldMatrixInvScaled);
            Lightlist.Add("OrbLight", thelight);



            thelight.UpdateLight();
        }

        void SetLights(bool on)
        {
            if (Lightlist != null)
            {
                foreach (var light in Lightlist.Values)
                {
                    light.LightOn = on;
                    light.GlareOn = on;
                    light.UpdateLight();
                }
            }
        }
        void SetLightsPos(Vector3 pos)
        {
            if (Lightlist != null)
            {
                foreach (var light in Lightlist.Values)
                {

                    light.Position = pos;
                    light.UpdateLight();
                }
            }
        }

        private bool LightMade;
        private const string SUBPART_NAME = "ORB";
        private bool FirstFind = true;
        private Vector3D OrbGlobalPos;
        private MyStringId orbMat = MyStringId.GetOrCompute("OrbParticleTransMat");
        private Dictionary<string, IMyModelDummy> tempDummyList = new Dictionary<string, IMyModelDummy>();
        private bool found = false;
        private IMyModelDummy ORB;
        private float OrbRadius;
        double Floaty(double amplitude, double oscilationsPerSecond, double timeSeconds)
        {
            return amplitude * Math.Sin(2 * Math.PI * oscilationsPerSecond * timeSeconds);
        }

        private void Float()
        {
            if (!found && m_block.IsFunctional)
            {
                tempDummyList.Clear();
                m_block.Model.GetDummies(tempDummyList);
                foreach (string s in tempDummyList.Keys)
                {
                    if (s.Contains(SUBPART_NAME))
                    {
                        ORB = tempDummyList[s];
                        found = true;
                    }
                }

            }

            try
            {
                if (m_isPowered && found && m_block.IsFunctional)
                {
                    if (!LightMade) 
                    {
                        if (!MyAPIGateway.Multiplayer.IsServer)
                        {
                            makeLight(ORB);
                        }
                        LightMade = true;
                    }
                    if (FirstFind)
                    {
                        FirstFind = false;

                        OrbGlobalPos = ORB.Matrix.Translation;
                        if (m_block.CubeGrid.GridSizeEnum == 0)
                        {
                            OrbRadius = 1f;
                        } 
                        else
                        {
                            OrbRadius = 0.2f;
                        }
                    }

                    OrbGlobalPos = ORB.Matrix.Up * (float)Floaty(0.5, 0.125f, MyAPIGateway.Session.GameplayFrameCounter / 60.0);
                    SetLightsPos(Vector3D.Transform(OrbGlobalPos, m_block.WorldMatrix));
                    MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
                    if (IsWorking && m_block.IsFunctional)
                    {
                        if (!MyAPIGateway.Multiplayer.IsServer)
                        {
                            SetLights(true);
                            MyTransparentGeometry.AddPointBillboard(orbMat, orbVecColor, Vector3D.Transform(OrbGlobalPos, m_block.WorldMatrix), OrbRadius, 0f);
                        }
                    }
                    else 
                    {
                        if (!MyAPIGateway.Multiplayer.IsServer)
                        {
                            SetLights(false);
                        }
                    }
                    
                    

                    var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
                    if (Vector3D.DistanceSquared(camPos,m_block.GetPosition()) > (250*250))
                    {
                        return;
                    }

                    
                    //OrbLocalPos *= Vector3D.Rotate(OrbLocalPos, Matrix.CreateFromAxisAngle(ORB.Matrix.Up, MathHelper.ToRadians(0.5f)));
                    //OrbLocalPos = Vector3D.Normalize(OrbLocalPos);
                    //ORB.PositionComp.SetLocalMatrix(ref subpartLocalMatrix);
                    

                    
                    //Vector3D orbPos = ORB.WorldMatrix.Translation + ORB.WorldMatrix.Up * (float)Floaty(0.0125, 0.125f, MyAPIGateway.Session.GameplayFrameCounter / 60.0);

                    

                }
                if (!m_isPowered && found && !MyAPIGateway.Multiplayer.IsServer)
                {
                    SetLights(false);
                }
            } catch (Exception e)
            {
                MyLog.Default.WriteLine("Len.ERORR: " + e);
            }

        }
    }
}

