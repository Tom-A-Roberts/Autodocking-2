using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class Waypoint
        {
            public Vector3D position;
            public Vector3D forward;
            public double required_accuracy = 0.1;
            /// <summary>
            /// 15 is fast but not great for doing final landing
            /// 5 is better for being accurate
            /// High max acceleration results in lots of joltyness at the end, however faster docking.
            /// </summary>
            public double maximumAcceleration = 5;
            /// <summary>
            /// This includes the "caution" variable already given in the main script.
            /// </summary>
            public double PercentageOfMaxAcceleration = 1;
            public bool RequireRotation = true;


            public Waypoint(Vector3D _pos, Vector3D _forward)
            {
                position = _pos;
                forward = _forward;
            }
        }
    }
}
