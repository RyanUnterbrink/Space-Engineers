using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Linq;
using System;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    partial class Control : MyGridProgram
    {
        #region
        bool startRC = true;
        ColInfo[] colors = new ColInfo[]
        {
            new ColInfo("Default", new Color(79, 99, 239)),
            new ColInfo("Purple", Color.BlueViolet),
            new ColInfo("Blue", Color.MidnightBlue),
            new ColInfo("Red", Color.Red),
            new ColInfo("Green", Color.DarkGreen),
            new ColInfo("Black", Color.Black),
            new ColInfo("White", Color.White)
        };
        Color sCol = new Color(0, 0, 0);
        Color cpCol = new Color(0, 0, 0);

        List<IMyTerminalBlock> hdw = new List<IMyTerminalBlock>();
        List<IMyShipController> sCtrls = new List<IMyShipController>();
        IMyTextSurface primSurf, secSurf;
        List<IMyCameraBlock> camsDw = new List<IMyCameraBlock>();
        List<IMyThrust> thstrs;
        List<IMyThrust> thstrsFw = new List<IMyThrust>();
        List<IMyThrust> thstrsBw = new List<IMyThrust>();
        List<IMyThrust> thstrsUw = new List<IMyThrust>();
        List<IMyThrust> thstrsDw = new List<IMyThrust>();
        List<IMyThrust> thstrsLw = new List<IMyThrust>();
        List<IMyThrust> thstrsRw = new List<IMyThrust>();
        List<IMyGyro> gyros;
        List<IMyFunctionalBlock> hovEngs = new List<IMyFunctionalBlock>();
        List<IMyJumpDrive> jDrvs;
        List<IMyLandingGear> landGear;
        List<IMyLightingBlock> lghts = new List<IMyLightingBlock>();
        List<IMyTextSurface> ctrlSurfs = new List<IMyTextSurface>();

        const float TICKTIME = 1f / 60;
        static float updateTime = TICKTIME;
        static float lifeTime = 0;
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
        bool ctrled = false;
        double iAcc = 25;
        struct BlackBox
        {
            public Info currInfo;
            public Info prevInfo;
            const double NULLALT = -70000;
            public struct Info
            {
                public Vector3 fw;
                public Vector3 vel;
                public double angVel;
                public double acc;
                public double alt;
                public double fallVel;
                public float time;

                public Info(Vector3 _fw, Vector3 _vel, double angV, double _acc, float _time, double _alt)
                {
                    fw = _fw;
                    vel = _vel;
                    angVel = angV;
                    acc = _acc;
                    time = _time;
                    alt = _alt;
                    fallVel = 0;
                }
            }

            public BlackBox(Vector3 fw, Vector3 vel, double angV, float time)
            {
                currInfo = new Info(fw, vel, angV, 0, time, 0);
                prevInfo = currInfo;
            }

            public Vector3 Fw
            {
                get
                {
                    return currInfo.fw;
                }
            }
            public Vector3 Vel
            {
                get
                {
                    return currInfo.vel;
                }
            }
            public double AngVel
            {
                get
                {
                    return currInfo.angVel;
                }
            }
            public double Acc
            {
                get
                {
                    return currInfo.acc;
                }
            }
            public double Alt
            {
                get
                {
                    return currInfo.alt;
                }
            }
            public double FallVel
            {
                get
                {
                    if (currInfo.alt != NULLALT && prevInfo.alt != NULLALT)
                        return (currInfo.alt - prevInfo.alt) / (currInfo.time - prevInfo.time);
                    return 0;
                }
            }
            public void AddInfo(Vector3 fw, Vector3 vel, double angV, float lifetime, double alt = NULLALT)
            {
                float t = lifeTime - currInfo.time;
                double acc = (vel.Length() - currInfo.vel.Length()) / t;
                if (angV > .01 && t != 0)
                    angV /= t;
                else
                    angV = 0;
                prevInfo = currInfo;
                currInfo = new Info(fw, vel, angV, acc, lifetime, alt);
            }
        }
        BlackBox bB;
        struct MoveInd
        {
            public float x;
            public float y;
            public float z;

            public MoveInd(int temp = 0)
            {
                x = 0;
                y = 0;
                z = 0;
            }
        }
        MoveInd moveInd = new MoveInd();
        struct RotInd
        {
            public float pi;
            public float ya;
            public float ro;

            public RotInd(int temp = 0)
            {
                pi = 0;
                ya = 0;
                ro = 0;
            }
        }
        RotInd rotInd = new RotInd();
        class GyroOri
        {
            int pi = 0;
            int ya = 0;
            int ro = 0;

            public GyroOri(MatrixD m, IMyGyro g)
            {
                Vector3D sFw, sBw, sUw, sDw, sLw, sRw, gFw, gLw;
                sFw = m.Forward;
                sBw = m.Backward;
                sUw = m.Up;
                sDw = m.Down;
                sLw = m.Left;
                sRw = m.Right;
                gFw = g.WorldMatrix.Forward;
                gLw = g.WorldMatrix.Left;
                if (gLw == sLw || gLw == sRw)
                    pi = gLw == sLw ? 1 : -1;
                else if (gFw == sLw || gFw == sRw)
                    pi = gFw == sLw ? 2 : -2;
                else if (gFw == sFw)
                    pi = gLw == sUw ? 3 : -3;
                else if (gFw == sBw)
                    pi = gLw == sDw ? 4 : -4;
                else if (gFw == sUw)
                    pi = gLw == sBw ? 5 : -5;
                else
                    pi = gLw == sFw ? 6 : -6;
                if (gLw == sUw || gLw == sDw)
                    ya = gLw == sDw ? 1 : -1;
                else if (gFw == sUw || gFw == sDw)
                    ya = gFw == sDw ? 2 : -2;
                else if (gFw == sFw)
                    ya = gLw == sLw ? 3 : -3;
                else if (gFw == sBw)
                    ya = gLw == sRw ? 4 : -4;
                else if (gFw == sLw)
                    ya = gLw == sBw ? 5 : -5;
                else
                    ya = gLw == sFw ? 6 : -6;
                if (gLw == sFw || gLw == sBw)
                    ro = gLw == sFw ? 1 : -1;
                else if (gFw == sFw || gFw == sBw)
                    ro = gFw == sFw ? 2 : -2;
                else if (gFw == sLw)
                    ro = gLw == sDw ? 3 : -3;
                else if (gFw == sRw)
                    ro = gLw == sUw ? 4 : -4;
                else if (gFw == sUw)
                    ro = gLw == sLw ? 5 : -5;
                else
                    ro = gLw == sRw ? 6 : -6;
            }

            public Vector3 Ori(Vector3D rotation)
            {
                Vector3 p, y, r;
                p = GetCnfg(pi, rotation.X);
                y = GetCnfg(ya, rotation.Y);
                r = GetCnfg(ro, rotation.Z);
                double nP, nY, nR;
                nP = p.X + y.X + r.X;
                nY = p.Y + y.Y + r.Y;
                nR = p.Z + y.Z + r.Z;
                return new Vector3(nP, nY, nR);
            }
            Vector3 GetCnfg(int cnfg, double rot)
            {
                switch (Math.Abs(cnfg))
                {
                    case 1:
                        if (cnfg > 0)
                            return new Vector3(rot, 0, 0);
                        return new Vector3(-rot, 0, 0);
                    case 2:
                        if (cnfg > 0)
                            return new Vector3(0, 0, rot);
                        return new Vector3(0, 0, -rot);
                    default:
                        if (cnfg > 0)
                            return new Vector3(0, rot, 0);
                        return new Vector3(0, -rot, 0);
                }
            }
        }
        List<GyroOri> gOris = new List<GyroOri>();
        bool thstOVR = false;
        double minThstOVR = .00000001f;
        List<string> uiMssgs = new List<string>();
        int maxUI = 5;
        const string hl = "\n---------------------\n";
        bool autoHov = false;
        float hovScanDis = 30;
        int gyrPow = 3;
        bool gravAlign = false;
        double altLock = 0;
        double cruiseCtrl = 0;
        bool steerCtrl = false;
        float steer = 0;
        int steerInput = 0;
        int landProc = 0;
        float landScanDis = 250;
        float landGearDeployDis = 2;
        bool autoLights = true;
        bool lghtsOn = false;
        struct ColInfo
        {
            public string name;
            public Color col;

            public ColInfo(string _name, Color _color)
            {
                name = _name;
                col = _color;
            }
        }
        int colIndex = 0;
        List<string> excludeHw = new List<string> { "Weapon", "Exclude" };
        bool setup = true;

        float Alpha
        {
            get
            {
                return VecCos(GetWrldFw(), bB.Vel) * 100;
            }
        }
        bool LandGearDown
        {
            get
            {
                foreach (var l in landGear)
                {
                    if (l.Enabled)
                        return true;
                }
                return false;
            }
        }
        bool IsLandLocked
        {
            get
            {
                foreach (var l in landGear)
                {
                    if (l.IsLocked)
                        return true;
                }
                return false;
            }
        }
        int ColIndex
        {
            get
            {
                return colIndex;
            }
            set
            {
                if (value >= colors.Length)
                    colIndex = 0;
                else
                    colIndex = value;
            }
        }

        IMyShipController SCtrl
        {
            get
            {
                if (!sCtrls[0].IsFunctional)
                {
                    if (sCtrls.Count > 0)
                    {
                        sCtrls.RemoveAt(0);
                        sCtrls.RemoveAll(sC => !sC.IsFunctional);
                        IMyShipController s = sCtrls.FirstOrDefault(sC => sC.IsUnderControl);
                        if (s == null)
                        {
                            s = sCtrls.FirstOrDefault(sC => sC.CustomData.Contains("Main"));
                            if (s == null)
                            {
                                s = sCtrls.FirstOrDefault(sC => sC is IMyCockpit);
                                if (s == null && sCtrls.Count > 0)
                                    s = sCtrls[0];
                            }
                        }
                        if (s != null)
                        {
                            SetSCtrl(s);
                            return s;
                        }
                    }
                    return null;
                }
                return sCtrls[0];
            }
        }
        IMyTextSurface PrimSurf
        {
            get
            {
                IMyFunctionalBlock s = (IMyFunctionalBlock)primSurf;
                if (primSurf == null || !s.IsFunctional)
                    primSurf = GetHUDSurf(" 1");
                return primSurf;
            }
        }
        IMyTextSurface SecSurf
        {
            get
            {
                IMyFunctionalBlock s = (IMyFunctionalBlock)secSurf;
                if (secSurf == null || !s.IsFunctional)
                    secSurf = GetHUDSurf(" 2");
                return secSurf;
            }
        }
        List<IMyThrust> Thstrs
        {
            get
            {
                thstrs.RemoveAll(t => !t.IsFunctional);
                return thstrs;
            }
        }
        List<IMyJumpDrive> JDrvs
        {
            get
            {
                jDrvs.RemoveAll(j => !j.IsFunctional);
                return jDrvs;
            }
        }
        List<IMyGyro> Gyros
        {
            get
            {
                gyros.RemoveAll(g => !g.IsFunctional);
                return gyros;
            }
        }
        List<IMyCameraBlock> CamsDw
        {
            get
            {
                camsDw.RemoveAll(c => !c.IsFunctional);
                return camsDw;
            }
        }
        List<IMyLightingBlock> Lghts
        {
            get
            {
                lghts.RemoveAll(l => !l.IsFunctional);
                return lghts;
            }
        }

        void Setup()
        {
            SetupSCtrls();
            if (SCtrl != null)
            {
                Echo($"{SCtrl.CustomName} Is Ship Controller. Starting Control Setup.");
                updateTime = TICKTIME;
                bB = new BlackBox(GetWrldFw(), SCtrl.GetShipVelocities().LinearVelocity, 0, lifeTime);
                tmrs.Add(new Tmr("Ctrl", Ticks(29), true, UDCtrl, true));
                tmrs.Add(new Tmr("HUD", Ticks(7), true, UDUI, true));
                SetupHdw();
                SetupCkptSurfs();
                SetupThstrs();
                SetupJDrvs();
                SetupGyros();
                SetupCams();
                SetupHov();
                SetupLandGear();
                SetupLghts();
                SetupSurfs();
                ToglAutoHov(false);
                LoadData();
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                setup = false;
                Echo("Control Setup Success!");
            }
        }
        void SetupSCtrls()
        {
            GridTerminalSystem.GetBlocksOfType(sCtrls);
            sCtrls.RemoveAll(sC => !sC.IsFunctional || excludeHw.Any(kW => sC.CustomData.Contains(kW)));
            IMyShipController s = sCtrls.FirstOrDefault(sC => sC.CustomData.Contains("Main"));
            if (s != null)
                SetSCtrl(s);
            if (sCtrls.Count > 0)
                Echo("Ship Controller Setup Success!");
            else
                Echo("Controls Setup Failed: No Ship Controller.");
        }
        void SetupHdw()
        {
            GridTerminalSystem.GetBlocksOfType(hdw);
            int n = hdw.Count;
            hdw.RemoveAll(hw => !hw.IsFunctional || excludeHw.Any(keyW => hw.CustomData.Contains(keyW)));
            Echo($"Excluded: {n - hdw.Count}.");
        }
        void SetupThstrs()
        {
            thstrs = GetHw<IMyThrust>();
            if (thstrs.Count > 0)
            {
                List<List<IMyThrust>> thstrTypes = new List<List<IMyThrust>>();
                List<IMyThrust> oldList = null;
                for (int i = thstrs.Count - 1; i >= 0; i--)
                {
                    if (thstrs[i].DefinitionDisplayNameText.Contains("Hover"))
                    {
                        thstrs.RemoveAt(i);
                        continue;
                    }
                    thstrs[i].Enabled = true;
                    thstrs[i].ThrustOverride = 0;
                    if (i == 0 || thstrs[i].DefinitionDisplayNameText != thstrs[i - 1].DefinitionDisplayNameText)
                        oldList = null;
                    for (int j = 0; j < thstrTypes.Count && oldList == null; j++)
                    {
                        if (thstrs[i].DefinitionDisplayNameText == thstrTypes[j][0].DefinitionDisplayNameText)
                            oldList = thstrTypes[j];
                    }
                    if (oldList == null)
                    {
                        List<IMyThrust> temp = new List<IMyThrust>();
                        temp.Add(thstrs[i]);
                        thstrTypes.Add(temp);
                    }
                    else
                        oldList.Add(thstrs[i]);
                }
                List<IMyThrust> newList = new List<IMyThrust>();
                for (int i = 0; i < thstrTypes.Count; i++)
                {
                    List<List<IMyThrust>> thstrOriGroups = new List<List<IMyThrust>>();
                    for (int j = 0; j < 6; j++)
                        thstrOriGroups.Add(new List<IMyThrust>());
                    for (int j = 0; j < thstrTypes[i].Count; j++)
                    {
                        Vector3I thstDir = thstrTypes[i][j].GridThrustDirection;
                        if (thstDir == Vector3I.Backward)
                        {
                            thstrOriGroups[0].Add(thstrTypes[i][j]);
                            thstrsFw.Add(thstrTypes[i][j]);
                        }
                        else if (thstDir == Vector3I.Forward)
                        {
                            thstrOriGroups[1].Add(thstrTypes[i][j]);
                            thstrsBw.Add(thstrTypes[i][j]);
                        }
                        else if (thstDir == Vector3I.Down)
                        {
                            thstrOriGroups[2].Add(thstrTypes[i][j]);
                            thstrsUw.Add(thstrTypes[i][j]);
                        }
                        else if (thstDir == Vector3I.Up)
                        {
                            thstrOriGroups[3].Add(thstrTypes[i][j]);
                            thstrsDw.Add(thstrTypes[i][j]);
                        }
                        else if (thstDir == Vector3I.Right)
                        {
                            thstrOriGroups[4].Add(thstrTypes[i][j]);
                            thstrsLw.Add(thstrTypes[i][j]);
                        }
                        else
                        {
                            thstrOriGroups[5].Add(thstrTypes[i][j]);
                            thstrsRw.Add(thstrTypes[i][j]);
                        }
                    }
                    List<IMyTerminalBlock> tTs = new List<IMyTerminalBlock>();
                    for (int j = 0; j < thstrOriGroups.Count; j++)
                    {
                        List<IMyThrust> ordList = new List<IMyThrust>();
                        for (int k = 0; k < thstrOriGroups[j].Count; k++)
                        {
                            if (j == 0)
                                ordList.Add(thstrOriGroups[j][k]);
                            else
                            {
                                int index = ordList.Count;
                                for (int l = index - 1; l >= 0; l--)
                                {
                                    if (!IsPosOrd(thstrOriGroups[j][k], ordList[l]))
                                        index = l;
                                    else
                                        break;
                                }
                                ordList.Insert(index, thstrOriGroups[j][k]);
                            }
                        }
                        foreach (var tT in ordList)
                            tTs.Add(tT);
                    }
                    if (Me.CustomData.Contains("Setup"))
                        RenameHw(tTs);
                    foreach (var tT in tTs)
                        newList.Add(tT as IMyThrust);
                }
                thstrs = newList;
                Echo("Thrusters Setup Success!");
            }
            else
                Echo("Thrusters Setup Failed.");
        }
        void SetupJDrvs()
        {
            jDrvs = GetHw<IMyJumpDrive>();
            if (jDrvs.Count > 0)
                Echo("Jump Drive Setup Success!");
            else
                Echo("Jump Drive Setup Failed.");
        }
        void SetupGyros()
        {
            gyros = GetHw<IMyGyro>();
            foreach (var g in gyros)
            {
                g.GyroOverride = false;
                g.Pitch = 0;
                g.Yaw = 0;
                g.Roll = 0;
                g.Enabled = true;
                gOris.Add(new GyroOri(SCtrl.WorldMatrix, g));
            }
            if (gOris.Count > 0)
                Echo("Gyroscopes Setup Success!");
            else
                Echo("Gyroscopes Setup Failed.");
        }
        void SetupCams()
        {
            List<IMyCameraBlock> cs = GetHw<IMyCameraBlock>();
            for (int i = 0; i < cs.Count; i++)
            {
                if (GetWrldDw() == cs[i].WorldMatrix.Forward && cs[i].CustomData.Contains("Hover"))
                {
                    camsDw.Add(cs[i]);
                    cs[i].EnableRaycast = startRC;
                }
            }
        }
        void SetupHov()
        {
            string s = "Hover Engines Setup ";
            try
            {
                GridTerminalSystem.GetBlockGroupWithName("hover engines").GetBlocksOfType(hovEngs);
                hovEngs.RemoveAll(h => !h.IsFunctional);
                
                if (hovEngs.Count > 0)
                {
                    if (camsDw.Count > 0)
                        Echo($"{s}Success!");
                    else
                        Echo($"{s}Failed: No Hover Cameras.");
                }
                else
                    Echo($"{s}Failed: No Hover Engines.");
            }
            catch (Exception)
            {
                Echo($"{s}Failed: No Hover Engine Group.");
            }
        }
        void SetupLandGear()
        {
            landGear = GetHw<IMyLandingGear>();
            if (landGear.Count > 0)
                Echo("Landing Gear Setup Success!");
            else
                Echo("Landing Gear Setup Failed.");
        }
        void SetupLghts()
        {
            if (Me.CustomData.Contains("Lights"))
            {
                GridTerminalSystem.GetBlockGroupWithName("lights").GetBlocksOfType(lghts);
                if (lghts.Count == 0)
                    lghts = GetHw<IMyLightingBlock>();
                if (lghts.Count > 0)
                {
                    foreach (var l in Lghts)
                    {
                        if (l is IMyInteriorLight)
                            l.SetValueFloat("Offset", 0);
                        else
                            l.SetValueFloat("Offset", 5);
                        l.Radius = float.MaxValue;
                        l.Intensity = float.MaxValue;
                    }
                    for (int i = lghts.Count - 1; i >= 0; i--)
                    {
                        if (lghts[i].DefinitionDisplayNameText.Contains("Spot"))
                        {
                            lghts[i].Color = GetColor("White");
                            lghts.RemoveAt(i);
                        }
                    }
                    tmrs.Add(new Tmr("Lights", Ticks(57), true, UDLghts, true));
                    Echo("Lights Setup Success!");
                    return;
                }
            }
            Echo("Lights Setup Failed");
        }
        void SetupSurfs()
        {
            primSurf = GetHUDSurf(" 1");
            secSurf = GetHUDSurf(" 2");
            List<IMyTextSurface> ss = GetHw<IMyTextSurface>();
            foreach (var s in ss)
            {
                IMyFunctionalBlock sf = s as IMyFunctionalBlock;
                if (sf.CustomData.Contains("Controls"))
                    ctrlSurfs.Add(s);
                if (sf.CustomData.Contains("Color"))
                {
                    s.BackgroundColor = cpCol;
                    s.FontColor = GetColor("Default");
                    s.ScriptBackgroundColor = cpCol;
                    s.ScriptForegroundColor = GetColor("Default");
                }
                else if (sf.CustomData.Contains("Camo"))
                {
                    s.BackgroundColor = sCol;
                    s.FontColor = sCol;
                    s.ScriptBackgroundColor = sCol;
                    s.ScriptForegroundColor = sCol;
                    s.ContentType = ContentType.TEXT_AND_IMAGE;
                }
            }
        }
        void SetupCkptSurfs()
        {
            List<IMyCockpit> cs = new List<IMyCockpit>();
            GridTerminalSystem.GetBlocksOfType(cs);
            foreach (var c in cs)
            {
                if (c.CustomData.Contains("Color"))
                {
                    for (int i = 0; true; i++)
                    {
                        IMyTextSurface s = c.GetSurface(i);
                        if (s != null)
                        {
                            s.ContentType = ContentType.SCRIPT;
                            s.ScriptBackgroundColor = cpCol;
                            s.ScriptForegroundColor = GetColor("Default");
                        }
                        else
                            break;
                    }
                }
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
                if (surf.IsFunctional && surf.CustomData.Contains("hudlcd:") && surf.CustomData.Contains(custom))
                {
                    s.ContentType = ContentType.TEXT_AND_IMAGE;
                    return s;
                }
            }
            return null;
        }

        void Args(string arg)
        {
            switch (arg)
            {
                case "ThrustOverride":
                    ToglThstOVR();
                    break;
                case "CruiseControl":
                    ToglCruiseCtrl();
                    break;
                case "SteerControl":
                    ToglStrCtrl();
                    break;
                case "AutoHover":
                    ToglAutoHov();
                    break;
                case "GravityAlign":
                    ToglGravAlgn();
                    break;
                case "AltitudeLock":
                    ToglAltLock();
                    break;
                case "GyroPower":
                    ToglGyroPow();
                    break;
                case "Land":
                    ToglLnding();
                    break;
                case "Takeoff":
                    ToglTakeoff();
                    break;
                case "AutoLights":
                    ToglAutoLghts();
                    break;
                case "LightColor":
                    SwitchLghtColor();
                    break;
                default:
                    break;
            }
        }

        void UD()
        {
            if (setup)
                Setup();
            else
            {
                lifeTime += updateTime;
                UDBBox();
                UDMoveInd();
                UDRotInd();
                UDTmrs();
            }
        }
        void UDTmrs()
        {
            for (int i = tmrs.Count - 1; i >= 0; i--)
            {
                if (tmrs[i].Run())
                    tmrs.RemoveAt(i);
            }
        }
        void UDBBox()
        {
            Vector3 fw = GetWrldFw();
            Vector3 vel = SCtrl.GetShipVelocities().LinearVelocity;
            double angVel = VecAngle(fw, bB.Fw);
            Vector3D plntPos;
            if (SCtrl.TryGetPlanetPosition(out plntPos))
            {
                double alt = (plntPos - SCtrl.GetPosition()).Length();
                bB.AddInfo(fw, vel, angVel, lifeTime, alt);
            }
            else
                bB.AddInfo(fw, vel, angVel, lifeTime);
        }
        void UDMoveInd()
        {
            Vector3 mI = SCtrl.MoveIndicator;
            moveInd.z = mI.Z;
            moveInd.y = mI.Y;
            moveInd.x = mI.X;
        }
        void UDRotInd()
        {
            Vector2 rI = SCtrl.RotationIndicator;
            rotInd.pi = rI.X;
            rotInd.ya = rI.Y;
            rotInd.ro = SCtrl.RollIndicator;
        }
        void UDThstrOVR()
        {
            if (thstOVR)
            {
                float vel = bB.Vel.Length();
                double acc = iAcc;
                if (vel > 100)
                    acc += Clamp(0, (float)iAcc, vel - 100);
                if (cruiseCtrl == 0)
                {
                    if (moveInd.z > 0)
                    {
                        OVRThstrs(thstrsBw, acc + GetGravCompThst(ThstrFw(thstrsBw[0])));
                        OVRThstrs(thstrsFw, minThstOVR);
                    }
                    else if (moveInd.z < 0)
                    {
                        OVRThstrs(thstrsFw, acc + GetGravCompThst(ThstrFw(thstrsFw[0])));
                        OVRThstrs(thstrsBw, minThstOVR);
                    }
                    else if (SCtrl.DampenersOverride)
                    {
                        OVRThstrs(thstrsFw, GetDampenThrust(ThstrFw(thstrsFw[0])) + GetGravCompThst(ThstrFw(thstrsFw[0])));
                        OVRThstrs(thstrsBw, GetDampenThrust(ThstrFw(thstrsBw[0])) + GetGravCompThst(ThstrFw(thstrsBw[0])));
                    }
                    else
                    {
                        OVRThstrs(thstrsFw, minThstOVR);
                        OVRThstrs(thstrsBw, minThstOVR);
                    }
                }
                if (altLock == 0 && landProc != 2)
                {
                    if (moveInd.y > 0)
                    {
                        OVRThstrs(thstrsUw, acc + GetGravCompThst(ThstrFw(thstrsUw[0])));
                        OVRThstrs(thstrsDw, minThstOVR);
                    }
                    else if (moveInd.y < 0)
                    {
                        OVRThstrs(thstrsDw, acc + GetGravCompThst(ThstrFw(thstrsDw[0])));
                        OVRThstrs(thstrsUw, minThstOVR);
                    }
                    else if (SCtrl.DampenersOverride)
                    {
                        OVRThstrs(thstrsUw, GetDampenThrust(ThstrFw(thstrsUw[0])) + GetGravCompThst(ThstrFw(thstrsUw[0])));
                        OVRThstrs(thstrsDw, GetDampenThrust(ThstrFw(thstrsDw[0])) + GetGravCompThst(ThstrFw(thstrsDw[0])));
                    }
                    else
                    {
                        OVRThstrs(thstrsDw, minThstOVR);
                        OVRThstrs(thstrsUw, minThstOVR);
                    }
                }
                if (moveInd.x > 0)
                {
                    OVRThstrs(thstrsRw, acc + GetGravCompThst(ThstrFw(thstrsRw[0])));
                    OVRThstrs(thstrsLw, minThstOVR);
                }
                else if (moveInd.x < 0)
                {
                    OVRThstrs(thstrsLw, acc + GetGravCompThst(ThstrFw(thstrsLw[0])));
                    OVRThstrs(thstrsRw, minThstOVR);
                }
                else if (SCtrl.DampenersOverride)
                {
                    OVRThstrs(thstrsLw, GetDampenThrust(ThstrFw(thstrsLw[0])) + GetGravCompThst(ThstrFw(thstrsLw[0])));
                    OVRThstrs(thstrsRw, GetDampenThrust(ThstrFw(thstrsRw[0])) + GetGravCompThst(ThstrFw(thstrsRw[0])));
                }
                else
                {
                    OVRThstrs(thstrsLw, minThstOVR);
                    OVRThstrs(thstrsRw, minThstOVR);
                }
            }
        }
        void UDAutoHov()
        {
            if (autoHov && landProc == 0 && CamsDw.Count > 0)
            {
                Tmr t = FindTmr("Auto Hover");
                t.TimeLength = Ticks((int)(Clamp(.01f, 1, 1 - ValPct(1, 300, bB.Vel.Length())) * 20));
                for (int i = 0; i < camsDw.Count; i++)
                {
                    if (camsDw[i].CanScan(hovScanDis))
                    {
                        MyDetectedEntityInfo info = camsDw[i].Raycast(hovScanDis);
                        if (!info.IsEmpty())
                        {
                            ToglHovEngs(true);
                            return;
                        }
                    }
                }
                ToglHovEngs(false);
            }
            else
                ToglAutoHov();
        }
        void UDGravAlign()
        {
            Vector3 grav = SCtrl.GetTotalGravity();
            float piCorr = 0;
            float roCorr = 0;
            if (grav.Length() > 0)
            {
                float sensitivity = 1 + Clamp(0, 1, ValPct(0, 500, bB.Vel.Length()) * 9);
                piCorr = VecCos(grav, GetWrldFw()) * sensitivity;
                roCorr = VecCos(grav, GetWrldRw()) * sensitivity;
            }
            OVRGyros(new Vector3(rotInd.pi - piCorr, rotInd.ya + steer * .03f, rotInd.ro - roCorr));
        }
        void UDAltLock()
        {
            double altDiff = 0;
            Vector3 gravity = SCtrl.GetTotalGravity();
            if (gravity.Length() != 0)
            {
                if (rotInd.pi < -30f || rotInd.pi > 30f)
                    altLock = bB.Alt + 10;
                altDiff = bB.Alt - altLock;
            }
            double fallVel = bB.FallVel;
            if (altDiff > 0)
            {
                if (fallVel > 0)
                {
                    OVRThstrs(thstrsDw, altDiff + fallVel);
                    OVRThstrs(thstrsUw, minThstOVR);
                }
                else
                {
                    OVRThstrs(thstrsDw, altDiff / 2);
                    OVRThstrs(thstrsUw, minThstOVR + (-fallVel / altDiff));
                }
            }
            else if (altDiff < 0)
            {
                if (fallVel < 0)
                {
                    OVRThstrs(thstrsUw, -altDiff - fallVel);
                    OVRThstrs(thstrsDw, minThstOVR);
                }
                else
                {
                    OVRThstrs(thstrsUw, -altDiff / 2);
                    OVRThstrs(thstrsDw, minThstOVR + (fallVel / -altDiff));
                }
            }
            else
            {
                OVRThstrs(thstrsUw, 0);
                OVRThstrs(thstrsDw, 0);
            }
        }
        void UDCruiseCtrl()
        {
            if (cruiseCtrl != 0)
            {
                if (moveInd.z != 0)
                {
                    if (thstOVR)
                    {
                        if (moveInd.z > 0)
                        {
                            OVRThstrs(thstrsBw, iAcc + GetGravCompThst(thstrsBw[0].WorldMatrix.Forward));
                            OVRThstrs(thstrsFw, minThstOVR);
                        }
                        else if (moveInd.z < 0)
                        {
                            OVRThstrs(thstrsFw, iAcc + GetGravCompThst(thstrsFw[0].WorldMatrix.Forward));
                            OVRThstrs(thstrsBw, minThstOVR);
                        }
                    }
                    else
                    {
                        OVRThstrs(thstrsFw, 0);
                        OVRThstrs(thstrsBw, 0);
                    }
                    cruiseCtrl = SCtrl.GetShipSpeed();
                }
                else
                {
                    double velDiff = bB.Vel.Length() - cruiseCtrl;
                    if (velDiff > 0)
                    {
                        OVRThstrs(thstrsBw, velDiff / 2);
                        OVRThstrs(thstrsFw, minThstOVR);
                    }
                    else
                    {
                        OVRThstrs(thstrsFw, -velDiff / 2);
                        OVRThstrs(thstrsBw, minThstOVR);
                    }
                }
            }
        }
        void UDSteerCtrl()
        {
            float ro = SCtrl.RollIndicator;
            float speedInfl = Clamp(.1f, 1, ValPct(0, 100, bB.Vel.Length()));
            float inc = 1;
            float max = 10;
            if (ro != 0 && steerInput <= 0)
            {
                steerInput = 3;
                if (ro > 0)
                    steer += inc;
                else if (ro < 0)
                    steer -= inc;
                if (steer > max)
                    steer = max;
                else if (steer < -max)
                    steer = -max;
            }
            else
                steerInput--;
            if (!gravAlign)
                OVRGyros(new Vector3(rotInd.pi, rotInd.ya + steer * .03f, 0));
        }
        void UDLnding()
        {
            string l = "Landing";
            string m = "Manual";
            double alt = 0;
            if (landProc != 3 && landProc != 4)
            {
                if (camsDw.Count > 0)
                {
                    foreach (var c in camsDw)
                    {
                        if (c.CanScan(landScanDis))
                        {
                            MyDetectedEntityInfo info = c.Raycast(landScanDis);
                            if (!info.IsEmpty())
                            {
                                alt = (c.GetPosition() - info.HitPosition.Value).Length();
                                if (alt < landScanDis)
                                {
                                    if (SCtrl.GetTotalGravity() == Vector3D.Zero)
                                    {
                                        if (landProc == 1)
                                            UI($"{m} {l}");
                                        else
                                            UI($"Auto {l} Failed: Gravity Not Detected. {m} {l}.");
                                        landProc = 3;
                                        break;
                                    }
                                }
                                else
                                {
                                    if (landProc == 1)
                                        UI($"{m} {l}");
                                    else
                                        UI($"Auto {l} Failed: Altitude Too High. {m} {l}.");
                                    landProc = 3;
                                    break;
                                }
                            }
                            else
                            {
                                if (landProc == 1)
                                    UI($"{m} {l}.");
                                else
                                    UI($"Auto {l} Failed: No {l} Position. {m} {l}.");
                                landProc = 3;
                                break;
                            }
                        }
                        else
                        {
                            if (landProc == 1)
                                UI($"{m} {l}.");
                            else
                                UI($"Auto {l} Failed: Insufficient Raycast. {m} {l}.");
                            landProc = 3;
                            break;
                        }
                    }
                }
                else
                {
                    if (landProc == 1)
                        UI($"{m} {l}.");
                    else
                        UI($"Auto {l} Failed: No Downward Cameras. {m} {l}.");
                    landProc = 3;
                }
            }
            switch (landProc)
            {
                case 1:
                    LndingApproach();
                    if (!gravAlign)
                        ToglGravAlgn(false);
                    UI($"Auto {l}.");
                    landProc = 2;
                    break;
                case 2:
                    if (bB.FallVel < 0)
                    {
                        float velLimit = ValPct(landGearDeployDis, landScanDis, (float)alt) * 4.7f + .3f;
                        if (-bB.FallVel < velLimit)
                            OVRThstrs(thstrsUw, SCtrl.GetTotalGravity().Length() + bB.FallVel);
                        else if (-bB.FallVel < alt / 2)
                            OVRThstrs(thstrsUw, SCtrl.GetTotalGravity().Length());
                        else
                            OVRThstrs(thstrsUw, SCtrl.GetTotalGravity().Length() + Math.Sqrt(-bB.FallVel));
                        OVRThstrs(thstrsDw, minThstOVR);
                    }
                    else
                    {
                        OVRThstrs(thstrsUw, minThstOVR);
                        OVRThstrs(thstrsDw, bB.FallVel);
                    }
                    if (alt < landGearDeployDis + Math.Abs(bB.FallVel))
                    {
                        if (!LandGearDown)
                            ToglLandingGear(true);
                        else if (IsLandLocked)
                            landProc = 5;
                    }
                    break;
                case 3:
                    LndingApproach();
                    if (gravAlign)
                        ToglGravAlgn(false);
                    if (!LandGearDown)
                        ToglLandingGear(true);
                    landProc = 4;
                    break;
                case 4:
                    if (IsLandLocked)
                        landProc = 5;
                    break;
                case 5:
                    landProc = 0;
                    ToglThstrs(thstrs, false);
                    OVRThstrs(thstrs, 0);
                    if (gravAlign)
                        ToglGravAlgn(false);
                    RemoveTmr(l);
                    UI($"{l} Successful!");
                    break;
                default:
                    break;
            }
        }
        void UDCtrl()
        {
            if (ctrled != SCtrl.IsUnderControl)
            {
                ctrled = !ctrled;
                if (ctrled)
                {
                    if (autoLights)
                        ChangeLghts(true, colors[colIndex].col);
                    UI($"{SCtrl.CubeGrid.CustomName} Active!");
                }
                else
                {
                    if (autoLights)
                        ChangeLghts(false, sCol);
                }
            }
            IMyShipController s;
            if (ctrled)
                s = sCtrls.FirstOrDefault(sC => sC != SCtrl && sC.IsUnderControl && sC.CustomData.Contains("Main"));
            else
                s = sCtrls.FirstOrDefault(sC => sC != SCtrl && sC.IsUnderControl);
            if (s != null)
                SetSCtrl(s);
        }
        void UDLghts()
        {
            if (Lghts.Count > 0)
            {
                if (!lghts[0].Enabled)
                {
                    if (lghtsOn)
                    {
                        lghtsOn = false;
                        ChangeLghts(false, sCol);
                    }
                }
                else if (!lghtsOn)
                {
                    lghtsOn = true;
                    ChangeLghts(true, colors[colIndex].col);
                }
            }
            else
            {
                autoLights = false;
                UI("Auto Lights Failed: Lights Destroyed.");
            }
        }
        void UDUI()
        {
            if (PrimSurf != null)
            {
                string s = "";
                Vector3D v = SCtrl.GetPosition();
                s += $"Coord : X: {v.X.ToString("n0")}  Y: {v.Y.ToString("n0")}  Z: {v.Z.ToString("n0")}.\n\n";
                if (bB.Vel.Length() > .5f)
                {
                    s += $"Velocity : {bB.Vel.Length().ToString("n2")}  m/s.\n\n";
                    s += $"Acceleration : {bB.Acc.ToString("n2")}  m/s^2.\n\n";
                }
                else
                {
                    s += $"Velocity : 0  m/s.\n\n";
                    s += $"Acceleration : 0  m/s^2.\n\n";
                }
                if (bB.Vel.Length() > .1f)
                {
                    s += $"Alpha : {Alpha.ToString("n2")}  %.\n\n";
                    v = Vector3D.Normalize(bB.Vel);
                    s += $"Direction : X: {v.X.ToString("n2")}  Y: {v.Y.ToString("n2")}  Z: {v.Z.ToString("n2")}.\n\n";
                }
                else
                {
                    s += "Alpha : 0  %.\n\n";
                    s += "Direction : X: 0  Y: 0  Z: 0.\n\n";
                }
                s += $"Angular : {bB.AngVel.ToString("n0")}  deg/s.\n\n";
                s += $"Movement : X: {moveInd.x.ToString("n0")}  Y: {moveInd.y.ToString("n0")}  Z: {moveInd.z.ToString("n0")}.\n\n";
                s += $"Rotation : P: {rotInd.pi.ToString("n0")}  Y: {rotInd.ya.ToString("n0")}  R: {rotInd.ro.ToString("n0")}.\n{hl}\n";
                s += $"Thrust Override : {OO(thstOVR)}.\n\n";
                if (cruiseCtrl != 0)
                    s += $"Cruise Control : ON : {cruiseCtrl.ToString("n2")}  m/s.\n\n";
                else
                    s += "Cruise Control : OFF.\n\n";
                if (steerCtrl)
                    s += $"Steer Control : ON : {steer.ToString("n0")}.\n\n";
                else
                    s += $"Steer Control : OFF.\n\n";
                s += $"Gravity Align : {OO(gravAlign)}.\n\n";
                s += $"Altitude Lock : {OO(altLock != 0)}.\n\n";
                string oo;
                if (hovEngs.Count > 0)
                {
                    oo = autoHov ? "AUTO" : "MAN";
                    s += $"Hover Engines : {oo} : ";
                    s += $"{OO(hovEngs[0].Enabled)}.\n{hl}\n";
                }
                else
                    s += $"{hl}\n";
                if (JDrvs.Count > 0)
                    s += $"Jump Drive : {jDrvs[0].Status}.\n\n";
                if (landGear.Count > 0)
                {
                    oo = (LandGearDown ? "Deployed" : "Retracted") + (IsLandLocked ? " : Locked" : " : Unlocked");
                    s += $"Landing Gear : {oo}.\n\n";
                }
                oo = autoLights ? "AUTO" : "MAN";
                if (lghts.Count > 0)
                    s += $"Lights : {oo} : {OO(lghts[0].Enabled)}.";
                primSurf.WriteText(s);
                foreach (var sf in ctrlSurfs)
                    sf.WriteText(s);
            }
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
        void ToglThstrs(List<IMyThrust> _thstrs, bool on)
        {
            foreach (var t in _thstrs)
                t.Enabled = on;
        }
        void OVRThstrs(List<IMyThrust> thstrs, double _acc)
        {
            float numTs = 0;
            foreach (var t in thstrs)
            {
                if (t.Enabled)
                    numTs++;
            }
            foreach (var t in thstrs)
                t.ThrustOverride = SCtrl.CalculateShipMass().TotalMass * (float)_acc / numTs;
        }
        double GetDampenThrust(Vector3D thrustDir)
        {
            double cos = VecCos(SCtrl.GetShipVelocities().LinearVelocity, thrustDir);
            if (cos > 0)
                return cos * iAcc;
            return minThstOVR;
        }
        double GetGravCompThst(Vector3D thrustDir)
        {
            Vector3D g = SCtrl.GetNaturalGravity();
            if (g != Vector3D.Zero)
            {
                double cos = VecCos(g, thrustDir);
                if (cos > 0)
                    return cos * g.Length();
            }
            return 0;
        }
        void ToglThstOVR(bool ui = true)
        {
            string r = "Thruster Override";
            if (thstrs.Count > 0)
            {
                thstOVR = !thstOVR;
                if (thstOVR)
                {
                    tmrs.Add(new Tmr(r, Ticks(1), true, UDThstrOVR, true));
                    if (ui)
                        UI($"{r} Enabled.");
                }
                else
                {
                    RemoveTmr(r);
                    if (cruiseCtrl == 0)
                    {
                        OVRThstrs(thstrsFw, 0);
                        OVRThstrs(thstrsBw, 0);
                    }
                    if (altLock == 0)
                    {
                        OVRThstrs(thstrsUw, 0);
                        OVRThstrs(thstrsDw, 0);
                    }
                    OVRThstrs(thstrsLw, 0);
                    OVRThstrs(thstrsRw, 0);
                    if (ui)
                        UI($"{r} Disabled.");
                }
            }
            else
                UI($"{r} Failed: No Thrusters.");
        }
        void ToglHovEngs(bool on)
        {
            foreach (var h in hovEngs)
                h.Enabled = on;
        }
        void ToglAutoHov(bool ui = true)
        {
            string r = "Auto Hover";
            if (hovEngs.Count > 0)
            {
                if (camsDw.Count > 0)
                {
                    autoHov = !autoHov;
                    if (autoHov)
                    {
                        bool scan = true;
                        bool rc = true;
                        foreach (var c in camsDw)
                        {
                            if (c.AvailableScanRange < hovScanDis)
                                scan = false;
                            if (!c.EnableRaycast)
                                rc = false;
                        }
                        if (scan && rc)
                        {
                            tmrs.Add(new Tmr(r, Ticks(20), true, UDAutoHov, true));
                            if (ui)
                                UI($"{r} Enabled.");
                        }
                        else if (!scan || !rc)
                        {
                            autoHov = false;
                            if (ui)
                                UI($"{r} Failed: Insufficient Raycast.");
                        }
                    }
                    else
                    {
                        RemoveTmr(r);
                        if (ui)
                            UI($"{r} Disabled.");
                    }
                }
                else if (ui)
                    UI($"{r} Failed: No Downward Cameras.");
            }
            else if (ui)
                UI($"{r} Failed: No Hover Engines.");
        }
        void ToglLandingGear(bool on)
        {
            foreach (var l in landGear)
            {
                if (!on)
                    l.Unlock();
                l.Enabled = on;
            }
        }
        void ToglGyroPow()
        {
            if (Gyros.Count > 0)
            {
                gyrPow++;
                if (gyrPow > 2)
                    gyrPow = 0;
                float pow = 1 / (float)Math.Pow(2, gyrPow);
                foreach (var g in Gyros)
                    g.GyroPower = pow;
                UI($"Gyroscope Power: {(pow * 100).ToString("n0")} %");
            }
            else
                UI("Gyroscope Power Failed: No Gyroscopes");
        }
        void ToglGravAlgn(bool ui = true)
        {
            string r = "Gravity Align";
            gravAlign = !gravAlign;
            if (gravAlign)
            {
                if (SCtrl.GetTotalGravity() != Vector3D.Zero)
                {
                    tmrs.Add(new Tmr(r, Ticks(1), true, UDGravAlign, true));
                    ToglGyroOVR(true);
                    if (ui)
                        UI($"{r} Enabled.");
                }
                else
                {
                    gravAlign = false;
                    if (ui)
                        UI($"{r} Failed: No Gravity.");
                }
            }
            else
            {
                RemoveTmr(r);
                if (!steerCtrl)
                    ToglGyroOVR(false);
                if (ui)
                    UI($"{r} Disabled.");
            }
        }
        void ToglAltLock(bool ui = true)
        {
            string r = "Altitude Lock";
            if (altLock == 0)
            {
                if (SCtrl.GetNaturalGravity() != Vector3D.Zero)
                {
                    altLock = bB.Alt + 10;
                    ToglThstrs(Thstrs, true);
                    tmrs.Add(new Tmr(r, Ticks(2), true, UDAltLock, true));
                    if (ui)
                        UI($"{r} Enabled.");
                }
                else if (ui)
                    UI($"{r} Failed: No Natural Gravity.");
            }
            else
            {
                altLock = 0;
                ToglThstrs(Thstrs, true);
                if (altLock == 0)
                {
                    OVRThstrs(thstrsUw, 0);
                    OVRThstrs(thstrsDw, 0);
                }
                RemoveTmr(r);
                if (ui)
                    UI($"{r} Disabled.");
            }
        }
        void ToglCruiseCtrl(bool ui = true)
        {
            string r = "Cruise Control";
            if (Thstrs.Count > 0)
            {
                if (cruiseCtrl == 0)
                {
                    if (bB.Vel.Length() < 1)
                    {
                        UI($"{r} Failed: Insufficient Speed.");
                        return;
                    }
                    cruiseCtrl = SCtrl.GetShipSpeed();
                    ToglThstrs(thstrsFw, true);
                    ToglThstrs(thstrsBw, true);
                    tmrs.Add(new Tmr(r, Ticks(3), true, UDCruiseCtrl, true));
                    if (ui)
                        UI($"{r} Enabled.");
                }
                else
                {
                    cruiseCtrl = 0;
                    if (!thstOVR)
                    {
                        OVRThstrs(thstrsFw, 0);
                        OVRThstrs(thstrsBw, 0);
                    }
                    RemoveTmr(r);
                    if (ui)
                        UI($"{r} Disabled.");
                }
            }
            else
                UI($"{r} Failed: No Thrusters.");
        }
        void ToglStrCtrl()
        {
            string s = "Steer Control";
            if (Gyros.Count > 0)
            {
                steerCtrl = !steerCtrl;
                if (steerCtrl)
                {
                    ToglGyroOVR(true);
                    tmrs.Add(new Tmr(s, Ticks(1), true, UDSteerCtrl, true));
                    UI($"{s} Enabled.");
                }
                else
                {
                    if (!gravAlign)
                        ToglGyroOVR(false);
                    RemoveTmr(s);
                    UI($"{s} Disabled.");
                }
            }
            else
            {
                steerCtrl = false;
                UI($"{s} Failed: No Gyroscopes");
            }
            steer = 0;
            steerInput = 0;
        }
        void ToglLnding()
        {
            string r = "Landing";
            if (landProc == 0)
            {
                if (!IsLandLocked)
                {
                    tmrs.Add(new Tmr(r, Ticks(4), true, UDLnding, true));
                    landProc = 1;
                }
                else
                    UI($"{r} Failed: Already Landed.");
            }
            else
            {
                ResetLnding();
                UI($"{r} Aborted.");
            }
        }
        void LndingApproach()
        {
            if (!thstOVR)
                ToglThstOVR(false);
            ToglThstrs(Thstrs, true);
            OVRThstrs(thstrs, 0);
            if (cruiseCtrl != 0)
                ToglCruiseCtrl(false);
            if (autoHov)
                ToglAutoHov(false);
            ToglHovEngs(false);
            if (altLock != 0)
                ToglAltLock(false);
        }
        void ResetLnding()
        {
            landProc = 0;
            Takeoff();
            RemoveTmr("Landing");
        }
        void ToglTakeoff()
        {
            if (IsLandLocked)
            {
                Takeoff();
                UI("Taking Off!");
            }
            else
                UI("Takeoff Failed: Currently Airborne.");
        }
        void Takeoff()
        {
            if (gravAlign)
                ToglGravAlgn(false);
            if (!autoHov)
                ToglAutoHov(false);
            ToglThstrs(Thstrs, true);
            OVRThstrs(thstrs, 0);
            ToglLandingGear(false);
        }
        void ToglGyroOVR(bool on)
        {
            foreach (var g in gyros)
            {
                g.GyroOverride = on;
                g.Pitch = 0;
                g.Yaw = 0;
                g.Roll = 0;
            }
        }
        void OVRGyros(Vector3 rot)
        {
            for (int i = Gyros.Count - 1; i >= 0; i--)
            {
                if (gyros[i].IsFunctional)
                {
                    Vector3 gO = gOris[i].Ori(rot);
                    gyros[i].Pitch = gO.X;
                    gyros[i].Yaw = gO.Y;
                    gyros[i].Roll = gO.Z;
                }
                else
                {
                    gyros.RemoveAt(i);
                    gOris.RemoveAt(i);
                }
            }
        }
        Color GetColor(string name)
        {
            Color color = Color.Black;
            foreach (var c in colors)
            {
                if (c.name == name)
                    return c.col;
            }
            return color;
        }
        void ToglAutoLghts()
        {
            string r = "Auto Lights";
            if (lghts.Count > 0)
            {
                autoLights = !autoLights;
                if (autoLights)
                    UI($"{r} Enabled.");
                else
                    UI($"{r} Disabled.");
            }
            else
                UI($"{r} Failed: No Lights.");
        }
        void SwitchLghtColor()
        {
            if (lghts.Count > 0)
            {
                ColIndex++;
                if (lghts[0].Enabled)
                {
                    Color c = colors[colIndex].col;
                    ChangeLghts(true, c);
                }
                UI($"Light Color Change: {colors[colIndex].name}.");
            }
            else
                UI("Light Color Change Failed: No Lights.");
        }
        void ChangeLghts(bool on, Color c)
        {
            foreach (var l in lghts)
            {
                l.Enabled = on;
                l.Color = c;
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
        void SetSCtrl(IMyShipController s)
        {
            sCtrls.Remove(s);
            sCtrls.Insert(0, s);
            foreach (var sC in sCtrls)
                sC.IsMainCockpit = false;
            s.IsMainCockpit = true;
            UI($"Ship Controller Changed: {s.CustomName}.");
        }
        Vector3D ThstrFw(IMyThrust t)
        {
            return t.WorldMatrix.Forward;
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
            return RoundVec(Vector3D.TransformNormal(b.GetPosition() - SCtrl.GetPosition(), MatrixD.Transpose(SCtrl.WorldMatrix)), 2);
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
        string OO(bool b)
        {
            return b ? "ON" : "OFF";
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
        float ValPct(float min, float max, float val)
        {
            return (val - min) / (max - min);
        }
        float Clamp(float min, float max, float val)
        {
            if (val < min)
                return min;
            else if (val > max)
                return max;
            return val;
        }
        float VecCos(Vector3 v1, Vector3 v2)
        {
            return Vector3.Dot(v1, v2) / (v1.Length() * v2.Length());
        }
        double VecAngle(Vector3 v1, Vector3 v2)
        {
            return Math.Acos(Vector3.Dot(v1, v2) / (v1.Length() * v2.Length())) * 180 / Math.PI;
        }
        float Ticks(int numTicks)
        {
            return TICKTIME * numTicks;
        }
        Vector3D GetWrldFw()
        {
            return SCtrl.WorldMatrix.Forward;
        }
        Vector3D GetWrldDw()
        {
            return SCtrl.WorldMatrix.Down;
        }
        Vector3D GetWrldRw()
        {
            return SCtrl.WorldMatrix.Right;
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
        void LoadData()
        {
            if (Storage.Length > 0)
            {
                string[] d = Storage.Split('|');
                LdColor(d[0]);
                Echo("Data Loaded.");
            }
            else
                Echo("No Data Loaded...");
        }
        void LdColor(string c)
        {
            colIndex = int.Parse(c);
            ChangeLghts(lghtsOn, colors[colIndex].col);
        }
        public void Save()
        {
            Storage = "";
            Storage += colIndex + "|";
        }
        #endregion

        // OVERVIEW
        //
        // Description: This script is designed to control the ship easier.
        // Author: RYAN UNTERBRINK
        // Date: 05/23/2022

        // READ THIS!!!
        //
        // For users of this script, take caution when changing variables
        // Please read the descriptions of these user variables to fully understand how to safely customize their values.

        // REQUIREMENTS
        //
        // 1. This grid must be a ship, not a station.
        // 2. There must be at least one cockpit or a remote control.

        // CUSTOM DATA
        //
        // Ship controller: "Main" - this ship controller will be used for calculations.
        // PB: "Setup" - for renaming thrusters.
        // PB: "Lights" - lights will be affected by the script.
        // Cameras: "Hover" - cameras downward will raycast and determine when hover engines whould be on (Auto Hover).
        // Sensors: "Security" - senses enemies.
        // Sound Blocks: "Security" - plays when sensor scan detects an enemy.
        // LCDs/Cockpits: "Color" - color the lcd(s) with default color.
        // LCDs: "Camo" - camoflauge that lcd.
        // LCDs: "Controls" - display the ship controls.
        // Etc.: "Exclude" - for blocks to be excluded from this script.
        //
        // Examples:
        // Main Color Lights
        //
        // Main Co/lor Lights
        //
        // Exclude

        // MODS
        //
        // 1.HUD LCD
        // The mod displays text from the LCD onto the HUD.
        // This script gathers data from ship and writes it to an LCD with this mod's custom data: hudlcd:{PosX}:{PosY}:{Fontsize}:{Color}:{Shadow}
        // The hud lcd with the smallest font size will be the primary lcd, and the secondary will be the next largest.
        // Each hud lcd will be sorted with the number that comes after the hud lcd custom data.
        // Examples:
        // hudlcd:-1:.75:1:white:1 1
        // hudlcd:-0.69:-0.43:1.5:white:1 2
        //
        // 2. Hover Engines
        // Hovers ship when close to ground.
        // In the terminal, put all the hover engines in a group named "hover engines".
        // Also, dedicate a camera facing downwards to measure the ground distance by putting "Hover" in the custom data for that camera.
        // This script will take that group and turn it on or off depending on how close you are to the ground.

        // SAVED DATA
        //
        // 1. Light Color
    }
}
