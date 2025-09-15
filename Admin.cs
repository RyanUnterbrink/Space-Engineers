using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using System;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Admin : MyGridProgram
    {
        #region
        bool startRC = true;

        List<IMyTerminalBlock> hdw = new List<IMyTerminalBlock>();
        List<List<IMyTerminalBlock>> hwLists = new List<List<IMyTerminalBlock>>();
        IMyTextSurface primSurf, secSurf;
        IMyRadioAntenna ant;
        IMyCameraBlock camFw, camDw;
        List<IMyCameraBlock> cams;
        List<IMySensorBlock> snsrs;
        List<IMySoundBlock> sounds;
        List<IMyLargeTurretBase> turrets;
        List<IMyTurretControlBlock> turretCtrlrs;
        List<IMyPowerProducer> power;
        List<IMyBatteryBlock> batts;
        List<IMySolarPanel> solPs;
        List<IMySearchlight> sLghts = new List<IMySearchlight>();

        List<IMyTerminalBlock> ordList = new List<IMyTerminalBlock>();
        int hwListIdx = 0;
        int hwIdx = 0;
        int ordIdx = 0;
        int organizeMax = 100;
        bool organizeFast = true;
        bool initSetup = false;

        const float TICKTIME = 1f / 60;
        static float updateTime = TICKTIME;
        static float lifeTime = 0;
        IMyTerminalBlock matrix;
        class Tmr
        {
            public event Action action;

            public string Name { get; set; }
            public bool On { get; private set; }
            public float TimeLength { get; set; }
            public float TimeElapsed { get; private set; }
            public bool Repeat { get; set; }

            public Tmr(string name = "", float timeLength = 0, bool repeat = false, Action _action = null, bool startNow = false)
            {
                Name = name;
                TimeLength = timeLength;
                TimeElapsed = 0;
                Repeat = repeat;
                action = _action;
                On = startNow;
            }
            public void Start()
            {
                if (!On)
                {
                    On = true;
                    TimeElapsed = 0;
                }
            }
            public bool Run()
            {
                if (On)
                {
                    TimeElapsed += updateTime;
                    if (TimeElapsed > TimeLength)
                    {
                        On = false;
                        action?.Invoke();

                        if (Repeat)
                            Restart();
                        else
                            return true;
                    }
                }
                return false;
            }
            public void Restart()
            {
                On = true;
                TimeElapsed = 0;
            }
        }
        List<Tmr> tmrs = new List<Tmr>();
        List<string> uiMssgs = new List<string>();
        int maxUI = 5;
        float antRadius = 50000;
        IMyBroadcastListener broadLis;
        IMyUnicastListener uniLis;
        List<string> wlTags = new List<string>();
        List<string> blTags = new List<string>();
        List<long> wlAddys = new List<long>();
        List<long> blAddys = new List<long>();
        string tag = "513";
        IMyTerminalBlock cmList, cmLog, cmSndr, coordsList, entList;
        const string hl = "\n---------------------\n";
        const string vl = " | ";
        struct EntityInfo
        {
            public string name;
            public string rltnshp;
            public Vector3 pos;
            public Vector3 vel;
            public float dis;
            public float time;
            public long id;
            public MyDetectedEntityType type;

            public bool IsHuman
            {
                get
                {
                    return type == MyDetectedEntityType.CharacterHuman;
                }
            }
            public bool IsOwner
            {
                get
                {
                    return rltnshp == "Owner";
                }
            }
            public bool IsEnemy
            {
                get
                {
                    return rltnshp == "Enemies";
                }
            }
            public string Info
            {
                get
                {
                    return $"{name}{vl}{type}{vl}{rltnshp}{vl}{dis.ToString("n0")}  m.";
                }
            }
            public string UIInfo
            {
                get
                {
                    return $"{name} : {dis.ToString("n0")}  m.";
                }
            }

            public EntityInfo(MyDetectedEntityInfo i, float _dis, float _time)
            {
                name = i.Name;
                id = i.EntityId;
                rltnshp = i.Relationship.ToString();
                type = i.Type;
                dis = _dis;
                pos = i.Position;
                vel = i.Velocity;
                time = _time;
            }
        }
        List<EntityInfo> eInfo = new List<EntityInfo>();
        struct Coord
        {
            public string id;
            public Vector3D coords;
            public double dis;
            public float alpha;

            public string Info
            {
                get
                {
                    return $"{id}{vl}X{vl}{coords.X.ToString("n0")}{vl}Y{vl}{coords.Y.ToString("n0")}{vl}Z{vl}{coords.Z.ToString("n0")}{vl}{alpha.ToString("n0")}  % {vl}{dis.ToString("n0")}  m.";
                }
            }
            public string UIInfo
            {
                get
                {
                    return $"{id} : {alpha.ToString("n0")}  % : {dis.ToString("n0")}  m.";
                }
            }

            public Coord(string _id, Vector3D _coords, float _alpha, double _dis)
            {
                id = _id;
                coords = _coords;
                alpha = _alpha;
                dis = _dis;
            }
        }
        List<Coord> coords = new List<Coord>();
        bool snsrScan = false;
        float scanDis = 50000;
        int maxHp = 0;
        float inv = 0;
        float maxInv = 0;
        float powOutput = 0;
        float powOutputMax = 0;
        float powStored = 0;
        float powStoredMax = 0;
        float powSolOutput = 0;
        float powSolOutputMax = 0;
        List<string> excludeHw = new List<string> { "Weapon", "Exclude" };
        bool setup = true;

        IMyTerminalBlock Matrix
        {
            get
            {
                if (matrix == null || !matrix.IsFunctional)
                    matrix = hdw.FirstOrDefault(h => h.IsFunctional && h.CustomData.Contains("Matrix"));
                return matrix;
            }
        }
        IMyTextSurface PrimSurf
        {
            get
            {
                IMyFunctionalBlock s = (IMyFunctionalBlock)primSurf;
                if (primSurf == null || !s.IsFunctional)
                    primSurf = GetHUDSurf(" 3");
                return primSurf;
            }
        }
        IMyTextSurface SecSurf
        {
            get
            {
                IMyFunctionalBlock s = (IMyFunctionalBlock)secSurf;
                if (secSurf == null || !s.IsFunctional)
                    secSurf = GetHUDSurf(" 4");
                return secSurf;
            }
        }
        IMyRadioAntenna Ant
        {
            get
            {
                if (ant == null || !ant.IsFunctional)
                {
                    List<IMyRadioAntenna> ants = GetHw<IMyRadioAntenna>();
                    ant = ants.FirstOrDefault(a => a.IsFunctional);
                    if (ant != null)
                    {
                        ant.Radius = antRadius;
                        ant.HudText = "";
                        ant.ShowShipName = true;
                    }
                }
                return ant;
            }
        }
        IMyCameraBlock CamFw
        {
            get
            {
                if (camFw == null || !camFw.IsFunctional)
                    camFw = GetCam(GetWrldFw(), "Fw");
                return camFw;
            }
        }
        IMyCameraBlock CamDw
        {
            get
            {
                if (camDw == null || !camDw.IsFunctional)
                    camDw = GetCam(GetWrldDw(), "Dw");
                return camDw;
            }
        }
        List<IMyCameraBlock> Cams
        {
            get
            {
                cams.RemoveAll(c => !c.IsFunctional);
                return cams;
            }
        }
        List<IMyPowerProducer> Power
        {
            get
            {
                int n = power.Count;
                power.RemoveAll(p => !p.IsFunctional);
                if (n != power.Count)
                {
                    power = GetHw<IMyPowerProducer>();
                    foreach (var p in power)
                        powOutputMax += p.MaxOutput;
                }
                return power;
            }
        }
        List<IMyBatteryBlock> Batts
        {
            get
            {
                int n = batts.Count;
                batts.RemoveAll(b => !b.IsFunctional);
                if (n != batts.Count)
                {
                    powStoredMax = 0;
                    foreach (var b in batts)
                        powStoredMax += b.MaxStoredPower;
                }
                return batts;
            }
        }
        List<IMySolarPanel> SolPs
        {
            get
            {
                int n = solPs.Count;
                solPs.RemoveAll(s => !s.IsFunctional);
                if (n != solPs.Count)
                {
                    powSolOutputMax = 0;
                    foreach (var s in solPs)
                        powSolOutputMax += s.MaxOutput;
                }
                return solPs;
            }
        }
        List<IMySensorBlock> Snsrs
        {
            get
            {
                snsrs.RemoveAll(s => !s.IsFunctional);
                return snsrs;
            }
        }
        List<IMySoundBlock> Sounds
        {
            get
            {
                sounds.RemoveAll(s => !s.IsFunctional);
                return sounds;
            }
        }

        int HP
        {
            get
            {
                hdw.RemoveAll(hw => !hw.IsFunctional);
                return hdw.Count;
            }
        }
        bool CamRC
        {
            get
            {
                return cams.FirstOrDefault(c => c.EnableRaycast) != null;
            }
        }
        List<EntityInfo> NearbyEnemies
        {
            get
            {
                List<EntityInfo> eI = new List<EntityInfo>();
                foreach (var e in eInfo)
                {
                    if (e.IsEnemy)
                        eI.Add(e);
                }
                return eI;
            }
        }
        List<T> GetHw<T>()
        {
            List<T> hwList = new List<T>();
            foreach (var hw in hdw)
            {
                if (hw is T && hw.IsFunctional)
                    hwList.Add((T)hw);
            }
            return hwList;
        }
        IMyTextSurface GetHUDSurf(string custom)
        {
            List<IMyTextSurface> sfs = GetHw<IMyTextSurface>();
            foreach (var s in sfs)
            {
                IMyFunctionalBlock surf = s as IMyFunctionalBlock;
                if (surf.IsFunctional && surf.CustomData.Contains("hudlcd:"))
                {
                    s.ContentType = ContentType.TEXT_AND_IMAGE;
                    if (surf.CustomData.Contains(custom))
                        return s;
                }
            }
            return null;
        }
        IMyCameraBlock GetCam(Vector3 dir, string code)
        {
            cams.RemoveAll(cm => !cm.IsFunctional);
            IMyCameraBlock c = cams.FirstOrDefault(cm => cm.WorldMatrix.Forward == dir && cm.CustomData.Contains(code));
            if (c != null)
                c.EnableRaycast = startRC;
            return c;
        }

        void Setup()
        {
            SetupHdw();
            SetupMatrix();
            if (matrix != null)
            {
                Echo("Starting Admin Setup.");
                updateTime = TICKTIME;
                tmrs.Add(new Tmr("HUD", Ticks(7), true, UDUI, true));
                tmrs.Add(new Tmr("Power", Ticks(15), true, UDPower, true));
                tmrs.Add(new Tmr("Inventory", Ticks(31), true, UDInv, true));
                SetupOrgHw();
                maxHp = hdw.Count;
                SetupPower();
                SetupComms();
                SetupCoords();
                SetupCams();
                SetupSnsrs();
                SetupSrchLghts();
                ResetSLghts(false);
                SetupAI();
                SetupSoundBlocks();
                SetupSecurity();
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                setup = false;
                Echo("Admin Setup Success!");
            }
        }
        void SetupHdw()
        {
            GridTerminalSystem.GetBlocksOfType(hdw);
            int n = hdw.Count;
            hdw.RemoveAll(hw => !hw.IsFunctional || excludeHw.Any(keyW => hw.CustomData.Contains(keyW)));
            Echo($"Excluded: {n - hdw.Count}.");
        }
        void SetupMatrix()
        {
            if (Matrix != null)
                Echo("Matrix Setup Success!");
            else
                Echo("Admin Setup Failed: No Matrix Block.");
        }
        void SetupOrgHw()
        {
            List<IMyTerminalBlock> oldList = null;
            for (int i = 0; i < hdw.Count; i++)
            {
                if (i == 0 || hdw[i].DefinitionDisplayNameText != hdw[i - 1].DefinitionDisplayNameText)
                    oldList = null;
                for (int j = 0; j < hwLists.Count && oldList == null; j++)
                {
                    if (hdw[i].DefinitionDisplayNameText == hwLists[j][0].DefinitionDisplayNameText)
                        oldList = hwLists[j];
                }
                if (oldList == null)
                {
                    List<IMyTerminalBlock> newList = new List<IMyTerminalBlock>()
                    {
                        hdw[i]
                    };
                    hwLists.Add(newList);
                }
                else
                    oldList.Add(hdw[i]);
            }
            if (Me.CustomData.Contains("Setup"))
                initSetup = true;
        }
        void SetupPower()
        {
            power = GetHw<IMyPowerProducer>();
            foreach (var p in power)
                powOutputMax += p.MaxOutput;
            batts = GetHw<IMyBatteryBlock>();
            foreach (var b in batts)
                powStoredMax += b.MaxStoredPower;
            solPs = GetHw<IMySolarPanel>();
            foreach (var s in solPs)
                powSolOutputMax += s.MaxOutput;
            UDPower();
            if (powOutput > 0 || powStored > 0)
                Echo("Power Setup Success!");
            else
                Echo("Power Setup Failed: No Power Blocks.");
        }
        void SetupComms()
        {
            string bu = "Broadcast/Unicast Setup ";
            if (Ant != null)
            {
                broadLis = IGC.RegisterBroadcastListener(tag);
                broadLis.SetMessageCallback();
                uniLis = IGC.UnicastListener;
                uniLis.SetMessageCallback();
                cmList = hdw.FirstOrDefault(h => h.CustomData.Contains("Comm List"));
                if (cmList != null)
                    UDCommList(new MyIGCMessage("", "", 0));
                cmLog = hdw.FirstOrDefault(h => h.CustomData.Contains("Comm Log"));
                if (cmLog != null)
                {
                    IMyTextSurface t = cmLog as IMyTextSurface;
                    if (t != null && !t.GetText().Contains($"Comm Log{hl}"))
                        t.WriteText($"Comm Log{hl}");
                    else if (t == null && !cmLog.CustomData.Contains(hl))
                        cmLog.CustomData = $"Comm Log{hl}";
                }
                cmSndr = hdw.FirstOrDefault(h => h.CustomData.Contains("Comm Sender"));
                if (cmSndr != null)
                    UDCommSndr();
                if (cmList != null && cmLog != null & cmSndr != null)
                {
                    tmrs.Add(new Tmr("Comms", Ticks(39), true, UDComms, true));
                    Echo($"{bu}Success!");
                }
                else
                    Echo($"{bu}Failed.");
            }
            else
                Echo($"{bu}Failed: No Antenna.");
        }
        void SetupCoords()
        {
            coordsList = hdw.FirstOrDefault(c => c.CustomData.Contains("Coords List"));
            if (coordsList != null)
            {
                IMyTextSurface t = coordsList as IMyTextSurface;
                if (t != null && !t.GetText().Contains($"Coords List{hl}"))
                    t.WriteText($"Coords List{hl}");
                tmrs.Add(new Tmr("Coords", Ticks(28), true, UDCoords, true));
                Echo("Coords Setup Success!");
            }
            else
                Echo("Coords Setup Failed.");
        }
        void SetupCams()
        {
            cams = GetHw<IMyCameraBlock>();
            cams.RemoveAll(c => !c.CustomData.Contains("CamScan"));
            cams.ForEach(c => c.EnableRaycast = startRC);
            if (cams.Count > 0)
                Echo("Cameras Setup Success!");
            else
                Echo("Cameras Setup Failed.");
        }
        void SetupSnsrs()
        {
            snsrs = new List<IMySensorBlock>();
            List<IMySensorBlock> ss = GetHw<IMySensorBlock>();
            foreach (var s in ss)
            {
                if (s.CustomData.Contains("Security"))
                {
                    s.FrontExtend = float.MaxValue;
                    s.BackExtend = float.MaxValue;
                    s.TopExtend = float.MaxValue;
                    s.BottomExtend = float.MaxValue;
                    s.RightExtend = float.MaxValue;
                    s.LeftExtend = float.MaxValue;
                    s.DetectAsteroids = false;
                    s.DetectEnemy = true;
                    s.DetectFloatingObjects = true;
                    s.DetectFriendly = true;
                    s.DetectLargeShips = true;
                    s.DetectNeutral = true;
                    s.DetectOwner = true;
                    s.DetectPlayers = true;
                    s.DetectSmallShips = true;
                    s.DetectStations = true;
                    s.DetectSubgrids = false;
                    s.PlayProximitySound = false;
                    s.Enabled = false;
                    snsrs.Add(s);
                }
            }
            if (snsrs.Count > 0)
                Echo("Sensors Setup Success!");
            else
                Echo("Sensors Setup Failed.");
        }
        void SetupSrchLghts()
        {
            sLghts = GetHw<IMySearchlight>();
            foreach (var l in sLghts)
            {
                l.Color = Color.Black;
                l.Radius = 0;
                l.Intensity = 0;
                l.EnableIdleMovement = false;
                l.AimingRadius = float.MaxValue;
            }
        }
        void SetupAI()
        {
            turrets = GetHw<IMyLargeTurretBase>();
            turretCtrlrs = GetHw<IMyTurretControlBlock>();
            if (turrets.Count > 0 || turretCtrlrs.Count > 0 || sLghts.Count > 0)
            {
                tmrs.Add(new Tmr("AI", Ticks(1), true, UDAI, true));
                Echo("AI Setup Success!");
            }
            else
                Echo("AI Setup Failed.");
        }
        void SetupSoundBlocks()
        {
            sounds = GetHw<IMySoundBlock>();
            foreach (var s in sounds)
            {
                if (s.CustomData.Contains("Security"))
                {
                    s.Volume = float.MaxValue;
                    s.Range = float.MaxValue;
                    s.LoopPeriod = 1.2f;
                    s.SelectedSound = "Alert 1";
                }
            }
            if (sounds.Count > 0)
                Echo("Sound Setup Success!");
            else
                Echo("Sound Setup Failed.");
        }
        void SetupSecurity()
        {
            tmrs.Add(new Tmr("Security", Ticks(37), true, UDSecurity, true));
            entList = hdw.FirstOrDefault(eL => eL.CustomData.Contains("Entity List"));
            if (cams.Count > 0 && snsrs.Count > 0 && sounds.Count > 0)
                Echo("Security Setup Success!");
            else
                Echo("Security Setup Failed.");
        }
        
        void Args(string arg)
        {
            if (arg.Contains("CamScan"))
            {
                string[] s = arg.Split(new string[] { vl }, StringSplitOptions.None);
                if (s.Length > 1)
                    CamScan(s[1]);
            }
            else
            {
                switch (arg)
                {
                    case "SendMessage":
                        SendMesg();
                        break;
                    case "AddCoords":
                        AddCoords(Vector3D.Zero);
                        break;
                    case "SensorScan":
                        ToglSnsrScan();
                        break;
                    case "CameraRaycast":
                        ToglCamRC();
                        break;
                    case "ResetSearchLights":
                        ResetSLghts();
                            break;
                    default:
                        break;
                }
            }
        }

        void UD()
        {
            if (setup)
                Setup();
            else if (!UDOrganizeHw())
            {
                lifeTime += updateTime;
                UDTmrs();
            }
        }
        bool UDOrganizeHw()
        {
            if (!initSetup || hwListIdx == hwLists.Count)
                return false;
            do
            {
                if (hwIdx == 0)
                {
                    Echo($"Organizing {hwLists[hwListIdx][hwIdx].DefinitionDisplayNameText} List.");
                    ordList.Add(hwLists[hwListIdx][hwIdx]);
                    hwIdx++;
                    organizeFast = hwLists[hwListIdx].Count < organizeMax;
                }
                else
                {
                    int idx = ordList.Count - ordIdx;
                    bool insert = false;
                    int k = idx - 1;
                    if (!IsPosOrd(hwLists[hwListIdx][hwIdx], ordList[k]))
                        idx = k;
                    else
                        insert = true;
                    if (k == 0)
                        insert = true;
                    if (insert)
                    {
                        ordList.Insert(idx, hwLists[hwListIdx][hwIdx]);
                        Echo($"Organized {hwLists[hwListIdx][hwIdx].DisplayNameText}.");
                        hwIdx++;
                        ordIdx = 0;
                    }
                    else
                        ordIdx++;
                }
                if (hwIdx == hwLists[hwListIdx].Count)
                {
                    RenameHw(ordList);
                    hwLists[hwListIdx] = ordList;
                    hwListIdx++;
                    hwIdx = 0;
                    ordList.Clear();
                }
                if (hwListIdx == hwLists.Count)
                    Echo("Renaming Success!");
            }
            while (organizeFast && hwListIdx < hwLists.Count);
            if (hwListIdx == hwLists.Count)
                RenameCams();
            return hwListIdx < hwLists.Count;
        }
        void UDTmrs()
        {
            for (int i = tmrs.Count - 1; i >= 0; i--)
            {
                if (tmrs[i].Run())
                    tmrs.RemoveAt(i);
            }
        }
        void UDComms()
        {
            if (broadLis != null && uniLis != null)
            {
                bool broad = false;
                bool uni = false;
                MyIGCMessage m = new MyIGCMessage();
                if (broadLis.HasPendingMessage)
                {
                    m = broadLis.AcceptMessage();
                    broad = true;
                }
                else if (uniLis.HasPendingMessage)
                {
                    m = uniLis.AcceptMessage();
                    uni = true;
                }
                if (broad || uni)
                {
                    UDCommList(m);
                    string s = (string)m.Data;
                    string[] mesg = s.Split(':');
                    if (mesg.Length > 1)
                    {
                        if (mesg[0].Contains("Arg:"))
                            Args(mesg[1]);
                        else
                            AddMesg(m, broad);
                    }
                }
            }
        }
        void UDCoords()
        {
            UDCoordsList(Vector3D.Zero, "");
        }
        void UDCommList(MyIGCMessage m)
        {
            string a = BlockRead(cmList);
            string[] tNA = a.Split(new string[] { hl }, StringSplitOptions.None);
            if (tNA.Length > 5)
            {
                string[] _tags = tNA[3].Split(new string[] { "\n" }, StringSplitOptions.None);
                wlTags.Clear();
                foreach (var ta in _tags)
                {
                    if (!ta.Contains("Tags:"))
                        wlTags.Add(ta);
                }
                if (m.Tag != "" && !wlTags.Contains(m.Tag))
                    wlTags.Add(m.Tag);
                if (!wlTags.Contains(tag))
                    wlTags.Add(tag);
                string[] b = tNA[1].Split(' ');
                if (b.Length > 1 && b[1] != tag && !wlTags.Contains(b[1]))
                {
                    wlTags.Add(b[1]);
                    tag = b[1];
                    broadLis = IGC.RegisterBroadcastListener(tag);
                }
                string[] bTags = tNA[4].Split(new string[] { "\n" }, StringSplitOptions.None);
                blTags.Clear();
                foreach (var ta in bTags)
                {
                    if (!ta.Contains("Tags"))
                        blTags.Add(ta);
                }
                string[] _addys = tNA[5].Split(new string[] { "\n" }, StringSplitOptions.None);
                wlAddys.Clear();
                foreach (var ad in _addys)
                {
                    if (!ad.Contains("White"))
                    {
                        long f = long.Parse(ad);
                        wlAddys.Add(f);
                    }
                }
                if (m.Source != 0 && !wlAddys.Contains(m.Source))
                    wlAddys.Add(m.Source);
                string[] bAddys = tNA[6].Split(new string[] { "\n" }, StringSplitOptions.None);
                blAddys.Clear();
                foreach (var ad in bAddys)
                {
                    if (!ad.Contains("Black"))
                    {
                        long f = long.Parse(ad);
                        blAddys.Add(f);
                    }
                }
            }
            string s = $"Comm List{hl}Tag: {tag}{hl}Address: {IGC.Me}{hl}Whitelist Tags:";
            foreach (var ta in wlTags)
                s += $"\n{ta}";
            s += $"{hl}Blacklist Tags:";
            foreach (var ad in blTags)
                s += $"\n{ad}";
            s += $"{hl}Whitelist Addresses:";
            foreach (var ad in wlAddys)
                s += $"\n{ad}";
            s += $"{hl}Blacklist Addresses:";
            foreach (var ad in blAddys)
                s += $"\n{ad}";
            BlockWrite(cmList, s);
        }
        void UDCommLog(string m)
        {
            string a = BlockRead(cmLog);
            string[] l = a.Split(new string[] { hl }, StringSplitOptions.None);
            if (l[0].Contains("Comm Log"))
            {
                a = $"Comm Log{hl}{m}{hl}";
                for (int i = 1; i < l.Length; i++)
                {
                    if (l[i] != "")
                        a += $"{l[i]}{hl}";
                }
            }
            BlockWrite(cmLog, a);
        }
        void UDCommSndr()
        {
            string s = $"Comm Sender{hl}Broadcast/Unicast(B/U): B{hl}Message: ";
            BlockWrite(cmSndr, s);
        }
        void UDCoordsList(Vector3D coord, string s)
        {
            if (coordsList != null)
            {
                string[] l = BlockRead(coordsList).Split(new string[] { hl }, StringSplitOptions.None);
                string cl = "Coords List";
                if (l[0].Contains("Coords"))
                {
                    cl += hl;
                    coords.Clear();
                    for (int i = 1; i < l.Length; i++)
                    {
                        string[] m = l[i].Split(new string[] { vl }, StringSplitOptions.None);
                        if (m.Length != 9)
                            break;
                        Vector3D v = new Vector3D();
                        if (m[1] == "X")
                            v.X = double.Parse(m[2]);
                        if (m[3] == "Y")
                            v.Y = double.Parse(m[4]);
                        if (m[5] == "Z")
                            v.Z = double.Parse(m[6]);
                        coords.Add(new Coord(m[0], v, CoordAlpha(v), Vector3D.Distance(Me.GetPosition(), v)));
                    }
                    if (coord != Vector3D.Zero)
                        coords.Insert(0, new Coord($"{s} {coords.Count + 1}", coord, CoordAlpha(coord), Vector3D.Distance(Me.GetPosition(), coord)));
                    foreach (var c in coords)
                        cl += $"{c.Info}{hl}";
                    BlockWrite(coordsList, cl);
                }
            }
        }
        void UDSecurity()
        {
            bool h = hdw.FirstOrDefault(hw => hw.IsBeingHacked) != null;
            bool p = Sounds.FirstOrDefault(s => s.IsSoundSelected) != null;
            if (h && !p)
                sounds.ForEach(snd => snd.Play());
            else if (!h && p)
                sounds.ForEach(snd => snd.Stop());
            for (int i = eInfo.Count - 1; i >= 0; i--)
            {
                if (lifeTime - eInfo[i].time > 3)
                    eInfo.RemoveAt(i);
            }
            if (entList != null)
            {
                string s = $"Entity List{hl}";
                foreach (var e in eInfo)
                    s += $"{e.Info}\n";
                BlockWrite(entList, s);
            }
        }
        void UDSnsrScan()
        {
            string ss = "Sensor Scan";
            if (Snsrs.Count > 0 && snsrScan)
            {
                foreach (var s in snsrs)
                {
                    s.Enabled = !snsrs[0].Enabled;
                    if (s.Enabled)
                    {
                        List<MyDetectedEntityInfo> es = new List<MyDetectedEntityInfo>();
                        s.DetectedEntities(es);
                        foreach (var e in es)
                            AddEntity(e);
                    }
                }
                Tmr t = FindTmr(ss);
                if (!snsrs[0].Enabled)
                    t.TimeLength = Ticks(40);
                else
                    t.TimeLength = Ticks(20);
            }
            else
            {
                snsrScan = false;
                UI($"{ss} Failed: Sensors Destroyed.");
            }
        }
        void UDAI()
        {
            float range = .05f;
            bool rL = false;
            foreach (var t in turretCtrlrs)
            {
                bool locked = t.CustomData.Contains("Lock");
                if (t.HasTarget)
                {
                    AddEntity(t.GetTargetedEntity());
                    rL = true;
                }
                else if (!t.IsUnderControl && !locked)
                {
                    float rtd = 180 / (float)Math.PI;
                    if (t.AzimuthRotor != null)
                    {
                        float azAng = t.AzimuthRotor.Angle * rtd;
                        if (azAng < -range || azAng > range)
                        {
                            if (azAng > 180)
                                azAng -= 360;
                            t.AzimuthRotor.TargetVelocityRPM = -azAng;
                        }
                        else
                        {
                            t.AzimuthRotor.TargetVelocityRad = 0;
                            t.AzimuthRotor.RotorLock = true;
                        }
                    }
                    if (t.ElevationRotor != null)
                    {
                        float elAng = t.ElevationRotor.Angle * rtd;
                        if (elAng < -range || elAng > range)
                        {
                            if (elAng > 180)
                                elAng -= 360;
                            t.ElevationRotor.TargetVelocityRPM = -elAng;
                        }
                        else
                        {
                            t.ElevationRotor.TargetVelocityRad = 0;
                            t.ElevationRotor.RotorLock = true;
                        }
                    }
                }
                else
                    rL = true;
                if (rL && !locked)
                {
                    if (t.AzimuthRotor.RotorLock)
                        t.AzimuthRotor.RotorLock = false;
                    if (t.ElevationRotor.RotorLock)
                        t.ElevationRotor.RotorLock = false;
                }
            }
            foreach (var t in turrets)
            {
                if (t.HasTarget)
                    AddEntity(t.GetTargetedEntity());
                else if (!t.IsUnderControl && !t.Enabled)
                    t.Azimuth = t.Elevation = 0;
            }
        }
        void UDPower()
        {
            powOutput = 0;
            foreach (var p in Power)
                powOutput += p.CurrentOutput;
            powStored = 0;
            foreach (var b in Batts)
                powStored += b.CurrentStoredPower;
            powSolOutput = 0;
            foreach (var s in SolPs)
                powSolOutput += s.CurrentOutput;
        }
        void UDInv()
        {
            inv = 0;
            maxInv = 0;
            foreach (var h in hdw)
            {
                if (h.HasInventory)
                {
                    inv += (float)h.GetInventory(0).CurrentVolume;
                    maxInv += (float)h.GetInventory(0).MaxVolume;
                }
            }
        }
        void UDUI()
        {
            if (PrimSurf != null)
            {
                string s = "";
                s += $"Function : {HP} / {maxHp}  HP.\n\n";
                if (Power.Count > 0)
                    s += $"Power : {powOutput.ToString("n2")} / {powOutputMax.ToString("n2")}  MW.\n\n";
                if (Batts.Count > 0)
                    s += $"Batteries : {(powStored / powStoredMax * 100).ToString("n2")}  %.\n\n";
                if (SolPs.Count > 0)
                    s += $"Solar Panels : {powSolOutput.ToString("n2")} / {powSolOutputMax.ToString("n2")}  MW.\n\n";
                s += $"Inventory : {inv.ToString("n3")} / {maxInv.ToString("n3")}  m^3\n\n";
                if (Ant != null)
                    s += $"Antenna : {OO(ant.Enabled)} : {ant.Radius.ToString("n0")}  m.\n{hl}\n";
                s += $"Raycast : {OO(CamRC)}.\n\n";
                if (CamFw != null)
                    s += $"Fw Scan : {(camFw.AvailableScanRange / scanDis).ToString("n2")}  X.\n\n";
                if (CamDw != null)
                    s += $"Dw Scan : {(camDw.AvailableScanRange / scanDis).ToString("n2")}  X.\n";
                if (Snsrs.Count > 0)
                    s += $"\nSensor Scan : {OO(snsrScan)}.\n{hl}\n";
                else
                    s += $"{hl}\n";
                s += $"Coords : {coords.Count}\n";
                for (int i = 0; i < 3 && i < coords.Count; i++)
                    s += $"\n{coords[i].UIInfo}\n";
                List<EntityInfo> ne = NearbyEnemies;
                if (ne.Count > 0)
                {
                    s += $"{hl}\nEnemy Alert : {ne.Count}\n";
                    foreach (var e in ne)
                        s += $"\n{e.UIInfo}\n";
                }
                primSurf.WriteText(s);
            }
        }

        void Broadcast(string m)
        {
            foreach (var t in wlTags)
                IGC.SendBroadcastMessage(t, m, TransmissionDistance.TransmissionDistanceMax);
        }
        void Unicast(string m)
        {
            foreach (var a in wlAddys)
                IGC.SendUnicastMessage(a, tag, m);
        }
        void SendMesg()
        {
            string s = BlockRead(cmSndr);
            string[] m = s.Split(new string[] { hl }, StringSplitOptions.None);
            if (m.Length > 2)
            {
                string[] m1 = m[1].Split(' ');
                if (m1.Length > 1 && m1[0].Contains("Broadcast/Unicast(B/U):"))
                {
                    UDCommList(new MyIGCMessage("", tag, 0));
                    MyIGCMessage igcM = new MyIGCMessage(m[2], tag, IGC.Me);
                    if (m1[1].Contains("B"))
                    {
                        Broadcast(m[2]);
                        AddMesg(igcM);
                        UI("Broadcast Message Sent");
                    }
                    else if (m1[1].Contains("U"))
                    {
                        Unicast(m[2]);
                        AddMesg(igcM, false);
                        UI("Unicast Message Sent");
                    }
                    UDCommSndr();
                }
            }
        }
        void AddMesg(MyIGCMessage m, bool bCast = true)
        {
            DateTime t = DateTime.Now;
            string bu = bCast ? "Broadcast" : "Unicast";
            bool b = m.Source == IGC.Me;
            string so = b ? "Me" : m.Source.ToString();
            string s = $"From: {so}\n{bu}: {t}\nTag: {m.Tag}";
            if (!bCast && b)
            {
                s += $"\nAddresses:";
                foreach (var a in wlAddys)
                    s += $"\n{a}";
            }
            s += $"\n{m.Data}";
            UDCommLog(s);
        }
        void CamScan(string code)
        {
            IMyCameraBlock c = cams.FirstOrDefault(cm => cm.CustomData.Contains(code));
            string f = $"{code} Scan";
            if (c != null)
            {
                if (c.CanScan(scanDis))
                {
                    MyDetectedEntityInfo e = c.Raycast(scanDis);
                    if (!e.IsEmpty())
                    {
                        AddCoords(e.Position, f);
                        AddEntity(e);
                        UI($"{f}: {e.Name}.");
                    }
                    else
                    {
                        AddCoords(Me.GetPosition() + c.WorldMatrix.Forward * scanDis, f);
                        UI($"{f}: Empty.");
                    }
                }
                else
                    UI($"{f} Failed: Insufficient Range.");
            }
            else
                UI($"{f} Failed: No {code} Camera.");
        }
        void ToglCamRC()
        {
            string r = "Camera Raycast ";
            if (Cams.Count > 0)
            {
                bool on = !CamRC;
                cams.ForEach(c => c.EnableRaycast = on);
                if (on)
                    UI($"{r}Enabled.");
                else
                    UI($"{r}Disabled.");
            }
            else
                UI($"{r}Failed: No Cameras.");
        }
        void ToglSnsrScan(bool ui = true)
        {
            string r = "Sensor Scan";
            if (Snsrs.Count > 0)
            {
                snsrScan = !snsrScan;
                if (snsrScan)
                {
                    tmrs.Add(new Tmr(r, Ticks(20), true, UDSnsrScan, true));
                    foreach (var s in snsrs)
                        s.Enabled = true;
                    if (ui)
                        UI($"{r} Enabled.");
                }
                else
                {
                    RemoveTmr(r);
                    foreach (var s in snsrs)
                        s.Enabled = false;
                    if (ui)
                        UI($"{r} Disabled.");
                }
            }
            else if (ui)
                UI($"{r} Failed: No Sensors.");
        }
        void UI(string m)
        {
            if (SecSurf != null)
            {
                if (uiMssgs.Count >= maxUI)
                {
                    uiMssgs.RemoveAt(uiMssgs.Count - 1);
                    RemoveTmr("UI");
                }
                uiMssgs.Insert(0, m);
                tmrs.Insert(0, new Tmr("UI", Ticks(150), false, HideUI, true));
                WriteUI();
            }
        }
        void WriteUI()
        {
            string s = "";
            for (int i = 0; i < maxUI; i++)
            {
                if (maxUI - i > uiMssgs.Count)
                    s += "\n";
                else
                    s += uiMssgs[maxUI - i - 1] + "\n";
            }
            secSurf.WriteText(s);
        }
        void HideUI()
        {
            if (SecSurf != null)
            {
                uiMssgs.RemoveAt(uiMssgs.Count - 1);
                WriteUI();
            }
        }
        Tmr FindTmr(string tName)
        {
            for (int i = tmrs.Count - 1; i >= 0; i--)
            {
                if (tmrs[i].Name == tName)
                    return tmrs[i];
            }
            return null;
        }
        void RemoveTmr(string tName)
        {
            for (int i = tmrs.Count - 1; i >= 0; i--)
            {
                if (tmrs[i].Name == tName)
                {
                    tmrs.RemoveAt(i);
                    break;
                }
            }
        }
        bool IsPosOrd(IMyTerminalBlock b1, IMyTerminalBlock b2)
        {
            Vector3D v1 = LocalOriPos(b1);
            Vector3D v2 = LocalOriPos(b2);
            if (v1.Z < v2.Z)
                return false;
            else if (v1.Z == v2.Z)
            {
                if (v1.Y > v2.Y)
                    return false;
                else if (v1.Y == v2.Y)
                {
                    if (v1.X < v2.X)
                        return false;
                }
            }
            return true;
        }
        Vector3D LocalOriPos(IMyTerminalBlock b)
        {
            return RoundVec(Vector3D.TransformNormal(b.GetPosition() - matrix.GetPosition(), MatrixD.Transpose(matrix.WorldMatrix)), 2);
        }
        void RenameHw(List<IMyTerminalBlock> bs)
        {
            for (int i = 0; i < bs.Count; i++)
            {
                int numZeros = bs.Count.ToString().Length - (i + 1).ToString().Length;
                string zeros = "";
                for (int j = 0; j < numZeros; j++)
                    zeros += "0";
                if (bs.Count == 1)
                    bs[i].CustomName = bs[i].DefinitionDisplayNameText;
                else
                    bs[i].CustomName = bs[i].DefinitionDisplayNameText + " " + zeros + (i + 1);
            }
        }
        void RenameCams()
        {
            List<IMyCameraBlock> cs = GetHw<IMyCameraBlock>();
            for (int i = 0; i < cs.Count; i++)
            {
                Vector3 dir = cs[i].WorldMatrix.Forward;
                string d = "";
                if (dir == GetWrldFw())
                    d = " Fw";
                else if (dir == -GetWrldFw())
                    d = " Bw";
                else if (dir == -GetWrldDw())
                    d = " Uw";
                else if (dir == GetWrldDw())
                    d = " Dw";
                else if (dir == -GetWrldRw())
                    d = " Lw";
                else if (dir == GetWrldRw())
                    d = " Rw";
                if (!cs[i].CustomName.Contains(d))
                    cs[i].CustomName += d;
            }
        }
        string OO(bool b)
        {
            return b ? "ON" : "OFF";
        }
        void AddEntity(MyDetectedEntityInfo info)
        {
            if (!info.IsEmpty())
            {
                Vector3 pos = info.HitPosition != null ? info.HitPosition.Value : info.Position;
                Vector3 dis = pos - Me.GetPosition();
                EntityInfo e = new EntityInfo(info, dis.Length(), lifeTime);
                for (int i = 0; i < eInfo.Count; i++)
                {
                    if (eInfo[i].id == info.EntityId)
                    {
                        eInfo[i] = e;
                        return;
                    }
                }
                if (e.id != Me.CubeGrid.EntityId)
                {
                    eInfo.Add(e);
                    if (e.IsEnemy)
                        UI("Enemy Detected!");
                }
            }
        }
        void AddCoords(Vector3D c, string s = "Coords")
        {
            if (c != Vector3D.Zero)
                UDCoordsList(c, s);
            else
                UDCoordsList(Me.GetPosition(), s);
            if (coords.Count > 0)
                UI($"{s} {coords[coords.Count - 1].id}.");
        }
        void ResetSLghts(bool ui = true)
        {
            if (sLghts.Count > 0)
            {
                foreach (var l in sLghts)
                    l.SetManualAzimuthAndElevation(0, 0);
                if (ui)
                    UI("Search Lights Reset.");
            }
        }
        Vector3D RoundVec(Vector3D v, int decimals)
        {
            return new Vector3D
            {
                X = Math.Round(v.X, decimals),
                Y = Math.Round(v.Y, decimals),
                Z = Math.Round(v.Z, decimals)
            };
        }
        string BlockRead(IMyTerminalBlock c)
        {
            string a = c.CustomData;
            IMyTextSurface t = c as IMyTextSurface;
            if (t != null)
                a = t.GetText();
            return a;
        }
        void BlockWrite(IMyTerminalBlock c, string m)
        {
            IMyTextSurface t = c as IMyTextSurface;
            if (t != null)
                t.WriteText(m);
            else
                c.CustomData = m;
        }
        float CoordAlpha(Vector3D coords)
        {
            return VecCos(GetWrldFw(), coords - Me.GetPosition()) * 100;
        }
        float Ticks(int numTicks)
        {
            return TICKTIME * numTicks;
        }
        float VecCos(Vector3 v1, Vector3 v2)
        {
            return Vector3.Dot(v1, v2) / (v1.Length() * v2.Length());
        }
        Vector3D GetWrldFw()
        {
            return Matrix.WorldMatrix.Forward;
        }
        Vector3D GetWrldDw()
        {
            return Matrix.WorldMatrix.Down;
        }
        Vector3D GetWrldRw()
        {
            return Matrix.WorldMatrix.Right;
        }

        public Program()
        {
            Setup();
        }
        public void Main(string argument, UpdateType updateSource)
        {
            UD();
            Args(argument);
        }
        #endregion

        // OVERVIEW
        //
        // Description: This script is designed to organize and manage functional blocks on this ship.
        // Author: RYAN UNTERBRINK
        // Date: 07/06/2022

        // CUSTOM DATA
        //
        // Orientable Block: "Matrix" - for orienting directions.
        // PB: "Setup" - for initial setup after build is complete. parts will be organized by position, renamed and numbered.
        // Cameras: "CamScan" - allows raycasting and scanning entities and coordinates. Also include a "|" and a code below to match to the toolbar and raycast scan with specific cameras. "Fw" is for CamFw and "Dw" is for CamDw.
        // Turret Controllers: "Lock" - locks the movement of turret controller elevation and azimuth rotors.
        // LCDs/Etc. "Comm List" - allows addresses to be stored here and manually edited and automatically updated after, and temporarily shown on LCD.
        // LCDs/Etc.: "Comm Log" - allows messages to be written to this block's custom data and temporarily shown on LCD.
        // LCDs/Etc.: "Comm Sender" - allows messages to be read from this block's custom data and temporarily shown on LCD.
        // LCDs/Etc.: "Entity List" - display the entities detected by the sensors and camera raycasting.
        // LCDs/Etc.: "Coords List" - display the coordinates that have been manually saved.
        // Etc.: "Exclude" - for blocks to be excluded from this script.
        //
        // Examples:
        //
        // CamScan | Fw
        //
        // CamScan | Dw 1

        // MODS
        //
        // 1.HUD LCD
        // The mod displays text from the LCD onto the HUD.
        // This script gathers data from ship and writes it to an LCD with this mod's custom data: hudlcd:{PosX}:{PosY}:{Fontsize}:{Color}:{Shadow}
        // The hud lcd with the smallest font size will be the primary lcd, and the secondary will be the next largest.
        // Each hud lcd will be sorted with the number that comes after the hud lcd custom data.
        // Examples:
        // hudlcd:.73:.75:1:white:1 3
        // hudlcd:.3:-.43:1.5:white:1 4

        // SAVED DATA
        //
        // N/A
    }
}
