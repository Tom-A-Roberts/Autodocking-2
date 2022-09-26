using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    internal partial class Program
    {
        public class IOHandler
        {
            private readonly Program parent_program;
            private string echoLine = "";
            public List<IMyTextSurface> output_LCDs = new List<IMyTextSurface>();
            public List<IMyTimerBlock> output_timers = new List<IMyTimerBlock>();
            public List<IMyTimerBlock> output_start_timers = new List<IMyTimerBlock>();

            public IOHandler(Program _parent_program)
            {
                parent_program = _parent_program;
                FindOutputBlocks();
            }

            public static double RoundToSignificantDigits(double d, int digits)
            {
                if (d == 0)
                    return 0;

                var scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(d))) + 1);
                return scale * Math.Round(d / scale, digits);
            }

            public void FindOutputBlocks()
            {
                output_start_timers = new List<IMyTimerBlock>();
                if (parent_program.force_timer_search_on_station)
                {
                    List<IMyTerminalBlock> t_search_blocks = new List<IMyTerminalBlock>();
                    parent_program.GridTerminalSystem.GetBlocks(t_search_blocks);
                    output_timers = new List<IMyTimerBlock>();

                    output_LCDs = new List<IMyTextSurface>();
                    foreach (var block in t_search_blocks)
                    {
                        if (block is IMyTimerBlock && block.CustomName.ToLower().Contains(parent_program.timer_tag))
                            output_timers.Add((IMyTimerBlock)block);
                        if (block is IMyTextSurface && block.CustomName.ToLower().Contains(parent_program.lcd_tag))
                            output_LCDs.Add((IMyTextSurface)block);
                        if (block is IMyTimerBlock && block.CustomName.ToLower().Contains(parent_program.start_timer_tag) && blockIsOnMyGrid(block))
                            output_start_timers.Add((IMyTimerBlock)block);
                    }
                }
                //
                else
                {
                    output_timers = new List<IMyTimerBlock>();
                    output_LCDs = new List<IMyTextSurface>();
                    foreach (var block in parent_program.blocks)
                    {
                        if (block is IMyTimerBlock && block.CustomName.ToLower().Contains(parent_program.timer_tag) && blockIsOnMyGrid(block))
                            output_timers.Add((IMyTimerBlock)block);
                        if (block is IMyTextSurface && block.CustomName.ToLower().Contains(parent_program.lcd_tag) && blockIsOnMyGrid(block))
                            output_LCDs.Add((IMyTextSurface)block);
                        if (block is IMyTimerBlock && block.CustomName.ToLower().Contains(parent_program.start_timer_tag) && blockIsOnMyGrid(block))
                            output_start_timers.Add((IMyTimerBlock)block);
                    }
                }

            }

            public bool blockIsOnMyGrid(IMyTerminalBlock block)
            {
                return block.IsSameConstructAs(parent_program.Me);
            }

            /// <summary>
            ///     Provides an error to the user then sets error state to true.<br />
            ///     This effectively stops the script entirely requiring it to be recompiled.
            /// </summary>
            /// <param name="ErrorString">The error published to the user.</param>
            public void Error(string ErrorString)
            {
                if (!parent_program.errorState) echoLine = "";
                Echo("ERROR:\n" + ErrorString);
                parent_program.errorState = true;
                parent_program.SafelyExit();
                EchoFinish();
            }

            public void WritePastableCoords(Vector3D coords, string coord_name = "0")
            {
                //GPS:Spug #1:53571.5:-26605.41:12103.89:
                Echo("GPS:" + coord_name + ":" + coords.X + ":" + coords.Y + ":" + coords.Z + ":");
            }


            /// <summary>
            ///     Adds the input string (or object) to an accumulated line.<br />
            ///     Use EchoFinish to output this accumulated line to the user.
            /// </summary>
            /// <param name="inp">Some object. This function applies .ToString() to it.</param>
            public void Clear()
            {
                echoLine = "";
            }

            public void Echo(object inp)
            {
                echoLine += inp + "\n";
            }

            public static string ConvertArg(string argument)
            {
                if (argument == "")
                    return "no argument";
                return argument;
            }

            public void OutputTimer()
            {
                if (parent_program.force_timer_search_on_station)
                {
                    FindOutputBlocks();
                }
                if (output_timers.Count > 0)
                    foreach (var timer in output_timers)
                        if (timer != null)
                            if (timer.IsWorking)
                                timer.Trigger();
            }
            public void OutputStartTimer()
            {
                if (output_start_timers.Count > 0)
                    foreach (var timer in output_start_timers)
                        if (timer != null)
                            if (timer.IsWorking)
                                timer.Trigger();
            }


            public void WaypointEcho(string arg, int count, string extra_output)
            {
                if (count == 0)
                {
                    Echo("RECORDING MODE\nRecording to argument: " + ConvertArg(arg) + ".\nPressing Run will record\nposition and rotation. To finish, press Run when docked.\n\nTo cancel, press Recompile.");
                }
                else
                {
                    Echo("RECORDING MODE\nRecorded " + count + " waypoints to argument: " + ConvertArg(arg) + extra_output + "\nPressing Run will record position and rotation again. To finish, press Run when docked.\n\nTo cancel, press Recompile.");
                }
            }

            /// <summary>
            ///     This will output the accumulated Echo line to the user.<br />
            ///     The bool parameter defines where the output will be:<br />
            ///     True - Only output into the programming block.<br />
            ///     False (default) - Output to both the programming block and an LCD with the name "LCD Panel".
            /// </summary>
            /// <param name="OnlyInProgrammingBlock">Defines where the output will be shown</param>
            public void EchoFinish(bool OnlyInProgrammingBlock = false, float fontSize = 1)
            {
                if (echoLine != "")
                {
                    if (parent_program.runningIssues.Length > 0) parent_program.runningIssues += "\n";
                    var echoString = "= Spug's Auto Docking 2.0 =\n\n" + parent_program.runningIssues + echoLine;
                    parent_program.Echo(echoString);
                    //parent_program.Me.GetSurface(0).ContentType = ContentType.TEXT_AND_IMAGE;
                    //parent_program.Me.GetSurface(0).WriteText(echoString);

                    if (!OnlyInProgrammingBlock && output_LCDs.Count > 0)
                        foreach (var surface in output_LCDs)
                            if (surface != null)
                            {
                                surface.ContentType = ContentType.TEXT_AND_IMAGE;
                                //surface.FontSize = fontSize;
                                //surface.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
                                surface.WriteText(echoString);
                            }
                    //IMyTextSurface surface = parent_program.GridTerminalSystem.GetBlockWithName("LCD Panel") as IMyTextSurface;

                    echoLine = "";
                }
            }


            public void OutputHomeLocations()
            {
                Echo("Known docking locations:");
                var count = 1;
                foreach (var currentHomeLocation in parent_program.homeLocations)
                {
                    //Echo("Station connector: " + currentHomeLocation.stationConnectorName);
                    //IMyShipConnector my_connector = (IMyShipConnector)parent_program.GridTerminalSystem.GetBlockWithId(currentHomeLocation.shipConnectorID);
                    //Echo("Ship connector: " + my_connector.CustomName);

                    var argStr = "- Location " + count + " arguments: ";
                    foreach (var arg in currentHomeLocation.arguments)
                    {
                        var arg_r = arg;
                        if (arg == "") arg_r = "NO ARG";
                        argStr += arg_r + ", ";
                    }

                    Echo(argStr.Substring(0, argStr.Length - 2));
                    count += 1;
                }
            }

            public string GetHomeLocationArguments(HomeLocation currentHomeLocation)
            {
                //Echo("\n   Home location Data:");
                //    Echo("Station connector: " + currentHomeLocation.stationConnectorName);
                //    IMyShipConnector my_connector = (IMyShipConnector)parent_program.GridTerminalSystem.GetBlockWithId(currentHomeLocation.shipConnectorID);
                //    Echo("Ship connector: " + my_connector.CustomName);
                var argStr = ""; // "Other arguments for this location: ";

                foreach (var arg in currentHomeLocation.arguments)
                {
                    var arg_r = arg;
                    if (arg == "") arg_r = "NO ARG";
                    argStr += arg_r + ", ";
                }

                if (argStr.Length > 2) argStr = argStr.Substring(0, argStr.Length - 2);
                return argStr;
            }

            public void DockingSequenceStartMessage(string argument)
            {
                //if (parent_program.scriptEnabled)
                //{
                //    if (argument == "")
                //    {
                //        Echo("RUNNING\nRe-starting docking sequence\nwith no argument.");
                //    }
                //    else
                //    {
                //        Echo("RUNNING\nRe-starting docking sequence\nwith new argument: " + argument);
                //    }
                //}
                //else
                //{
                if (argument == "")
                    Echo("RUNNING\nAttempting docking sequence\nwith no argument.");
                else
                    Echo("RUNNING\nAttempting docking sequence\nwith argument: " + argument);
                //}
            }
        }
    }
}