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
        public class IOHandler
        {
            readonly Program parent_program;
            private string echoLine = "";

            public IOHandler(Program _parent_program)
            {
                parent_program = _parent_program;
            }
            /// <summary>
            /// Provides an error to the user then sets error state to true.<br />
            /// This effectively stops the script entirely requiring it to be recompiled.
            /// </summary>
            /// <param name="ErrorString">The error published to the user.</param>
            public void Error(string ErrorString)
            {
                if (!parent_program.errorState)
                {
                    echoLine = "";
                }
                Echo("ERROR:\n" + ErrorString);
                parent_program.errorState = true;
                parent_program.SafelyExit();
                EchoFinish(false);
            }

            /// <summary>
            /// Adds the input string (or object) to an accumulated line.<br />
            /// Use EchoFinish to output this accumulated line to the user.
            /// </summary>
            /// <param name="inp">Some object. This function applies .ToString() to it.</param>
            public void Echo(Object inp)
            {
                echoLine += inp.ToString() + "\n";
            }

            /// <summary>
            /// This will output the accumulated Echo line to the user.<br />
            /// The bool parameter defines where the output will be:<br />
            /// True - Only output into the programming block.<br />
            /// False (default) - Output to both the programming block and an LCD with the name "LCD Panel".
            /// </summary>
            /// <param name="OnlyInProgrammingBlock">Defines where the output will be shown</param>
            public void EchoFinish(bool OnlyInProgrammingBlock = false)
            {
                if (echoLine != "")
                {
                    parent_program.Echo(echoLine);
                    if (!OnlyInProgrammingBlock)
                    {
                        IMyTextSurface surface = parent_program.GridTerminalSystem.GetBlockWithName("LCD Panel") as IMyTextSurface;
                        if (surface != null)
                        {
                            surface.ContentType = ContentType.TEXT_AND_IMAGE;
                            surface.FontSize = 1;
                            surface.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
                            surface.WriteText(echoLine);
                        }
                        echoLine = "";
                    }
                }
            }
        }
    }
}
