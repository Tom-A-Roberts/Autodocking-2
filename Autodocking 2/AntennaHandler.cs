using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    internal partial class Program
    {
        public class AntennaHandler
        {
            private const string _responseTag = "Spug's position update response";
            private const string _outgoingRequestTag = "Spug's position update request";
            private const string _recallRequestTag = "Spug's recall request";
            private readonly IMyBroadcastListener _myBroadcastListener;
            private readonly IMyUnicastListener _myUnicastListener;
            public IMyRadioAntenna antenna;
            public List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            public Program parent_program;

            public AntennaHandler(Program _program)
            {
                parent_program = _program;

                _myBroadcastListener = parent_program.IGC.RegisterBroadcastListener(_recallRequestTag);
                _myUnicastListener = parent_program.IGC.UnicastListener;
                //_myUnicastListener = parent_program.IGC.RegisterBroadcastListener(_responseTag);
                _myUnicastListener.SetMessageCallback("UNICAST");
                _myBroadcastListener.SetMessageCallback(_recallRequestTag);
            }

            public string CheckAntenna()
            {
                var antenna_found_success = 0;

                parent_program.GridTerminalSystem.GetBlocks(blocks);

                foreach (var block in blocks)
                    if (block is IMyRadioAntenna && blockIsOnMyGrid(block))
                    {
                        var my_antenna = (IMyRadioAntenna) block;
                        if (antenna_found_success < 1)
                        {
                            antenna_found_success = 1;
                            antenna = my_antenna;
                        }

                        if (my_antenna.Enabled && antenna_found_success < 2)
                        {
                            antenna_found_success = 2;
                            antenna = my_antenna;
                        }

                        if (my_antenna.EnableBroadcasting && my_antenna.Enabled && antenna_found_success < 3)
                        {
                            antenna_found_success = 3;
                            antenna = my_antenna;
                        }
                    }

                if (antenna_found_success == 3)
                    return "Antenna found:\nReady for use with the\noptional home script.";
                return "";
            }

            public void SendPositionUpdateRequest(long target_platform)
            {
                var target_connector_id = parent_program.systemsAnalyzer.currentHomeLocation.stationConnectorID;
                var target_grid_id = parent_program.systemsAnalyzer.currentHomeLocation.stationGridID;
                parent_program.IGC.SendBroadcastMessage(_outgoingRequestTag,
                    target_connector_id + ";" + target_grid_id);
            }

            public bool blockIsOnMyGrid(IMyTerminalBlock block)
            {
                return block.IsSameConstructAs(parent_program.Me);
            }

            public void HandleMessage()
            {
                //handle broadcasts:
                while (_myBroadcastListener.HasPendingMessage)
                {
                    var myIGCMessage = _myBroadcastListener.AcceptMessage();
                    if (myIGCMessage.Tag == _recallRequestTag)
                    {
                        var data = myIGCMessage.Data.ToString();
                        var data_parts = data.Split(';');
                        var arg = data_parts[0];

                        long sourceGrid = 0;
                        long.TryParse(data_parts[1], out sourceGrid);

                        parent_program.shipIOHandler.Echo("Broadcast received. This ship has been ordered to dock.");

                        var arg_test = parent_program.FindHomeLocation(arg);
                        if (arg_test != null && sourceGrid != parent_program.Me.CubeGrid.EntityId)
                            parent_program.Main(arg, UpdateType.Script);
                    }
                }


                // handle unicasts:
                var bFoundMessages = false;
                do
                {
                    bFoundMessages = false;
                    if (_myUnicastListener.HasPendingMessage)
                    {
                        bFoundMessages = true;
                        var msg = _myUnicastListener.AcceptMessage();
                        if (msg.Tag == _responseTag)
                            ParsePositionalResponse(msg.Data.ToString());
                        //parent_program.shipIOHandler.Echo("Unicast received. Data: " + .ToString());
                        //parent_program.shipIOHandler.Echo("Tag: " + msg.Tag.ToString());
                    }
                } while (bFoundMessages);
            }

            private void ParsePositionalResponse(string data)
            {
                //parent_program.shipIOHandler.Echo("Raw data:\n" + data);

                var data_parts = data.Split(';');

                if (data_parts.Length == 1)
                {
                    //Therefore an error has occurred.
                    //Print the error.
                    parent_program.shipIOHandler.Clear();
                    parent_program.shipIOHandler.Echo(data);
                    parent_program.SafelyExit();
                    parent_program.shipIOHandler.EchoFinish();
                }
                else if (data_parts.Length > 1)
                {
                    parent_program.hasConnectionToAntenna = true;

                    //messages_recieved += 1;
                    //parent_program.runningIssues = "Recieved: " + messages_recieved.ToString();
                    var result =
                        parent_program.systemsAnalyzer.currentHomeLocation.UpdateDataFromOptionalHomeScript(data_parts);

                    if (result.Length > 0) parent_program.runningIssues = result;
                }
            }
        }
    }
}