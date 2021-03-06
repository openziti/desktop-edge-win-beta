﻿using System;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Security.AccessControl;

using Newtonsoft.Json;
using NLog;

using ZitiDesktopEdge.DataStructures;

namespace ZitiDesktopEdge.Server {
    public class IPCServer {
        public const string PipeName = @"OpenZiti\ziti-monitor\ipc";
        public const string EventPipeName = @"OpenZiti\ziti-monitor\events";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static int BUFFER_SIZE = 16 * 1024;

        private JsonSerializer serializer = new JsonSerializer() { Formatting = Formatting.None };
        private string ipcPipeName;
        private string eventPipeName;

        public delegate string CaptureLogsDelegate();
        public CaptureLogsDelegate CaptureLogs { get; set; }

        public IPCServer() {
            this.ipcPipeName = IPCServer.PipeName;
            this.eventPipeName = IPCServer.EventPipeName;
        }

        async public Task startIpcServer() {
            int idx = 0;

            // Allow AuthenticatedUserSid read and write access to the pipe. 
            PipeSecurity pipeSecurity = new PipeSecurity();
            var id = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            pipeSecurity.SetAccessRule(new PipeAccessRule(id, PipeAccessRights.CreateNewInstance | PipeAccessRights.ReadWrite, AccessControlType.Allow));

            while (true) {
                try {
                    var ipcPipeServer = new NamedPipeServerStream(
                        ipcPipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                        BUFFER_SIZE,
                        BUFFER_SIZE,
                        pipeSecurity);

                    await ipcPipeServer.WaitForConnectionAsync();
                    Logger.Debug("Total ipc clients now at: {0}", ++idx);
                    _ = Task.Run(async () => {
                        try {
                            await handleIpcClientAsync(ipcPipeServer);
                        } catch(Exception icpe) {
                            Logger.Error(icpe, "Unexpected erorr in handleIpcClientAsync");
                        }
                        idx--;
                        Logger.Debug("Total ipc clients now at: {0}", idx);
                    });
                } catch (Exception pe) {
                    Logger.Error(pe, "Unexpected erorr when connecting a client pipe.");
                }
            }
        }
        async public Task startEventsServer() {
            int idx = 0;

            // Allow AuthenticatedUserSid read and write access to the pipe. 
            PipeSecurity pipeSecurity = new PipeSecurity();
            var id = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            pipeSecurity.SetAccessRule(new PipeAccessRule(id, PipeAccessRights.CreateNewInstance | PipeAccessRights.ReadWrite, AccessControlType.Allow));

            while (true) {
                try {
                    var eventPipeServer = new NamedPipeServerStream(
                        eventPipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                        BUFFER_SIZE,
                        BUFFER_SIZE,
                        pipeSecurity);

                    await eventPipeServer.WaitForConnectionAsync();
                    Logger.Debug("Total event clients now at: {0}", ++idx);
                    _ = Task.Run(async () => {
                        try {
                            await handleEventClientAsync(eventPipeServer);
                        } catch (Exception icpe) {
                            Logger.Error(icpe, "Unexpected erorr in handleEventClientAsync");
                        }
                        idx--;
                        Logger.Debug("Total event clients now at: {0}", idx);
                    });
                } catch (Exception pe) {
                    Logger.Error(pe, "Unexpected erorr when connecting a client pipe.");
                }
            }
        }

        async public Task handleIpcClientAsync(NamedPipeServerStream ss) {
            using (ss) {
                try {
                    StreamReader reader = new StreamReader(ss);
                    StreamWriter writer = new StreamWriter(ss);

                    string line = await reader.ReadLineAsync();

                    while (line != null) {
                        await processMessageAsync(line, writer);
                        line = await reader.ReadLineAsync();
                    }

                    Logger.Debug("handleIpcClientAsync is complete");
                } catch (Exception e) {
                    Logger.Error(e, "Unexpected erorr when reading from or writing to a client pipe.");
                }
            }
        }

        async public Task handleEventClientAsync(NamedPipeServerStream ss) {
            using (ss) {

                StreamWriter writer = new StreamWriter(ss);
                EventHandler eh = async (object sender, EventArgs e) => {
                    await writer.WriteLineAsync(sender.ToString());
                    await writer.FlushAsync();
                };

                ServiceStatusEvent status = new ServiceStatusEvent() {
                    Code = 0,
                    Error = null,
                    Message = "Success",
                    Status = ServiceActions.ServiceStatus()
                };
                await writer.WriteLineAsync(JsonConvert.SerializeObject(status));
                await writer.FlushAsync();

                EventRegistry.MyEvent += eh;
                try {
                    StreamReader reader = new StreamReader(ss);
                    
                    string line = await reader.ReadLineAsync();
                    while (line != null) {
                        await processMessageAsync(line, writer);
                        line = await reader.ReadLineAsync();
                    }

                    Logger.Debug("handleEventClientAsync is complete");
                } catch (Exception e) {
                    Logger.Error(e, "Unexpected erorr when reading from or writing to a client pipe.");
                }
                EventRegistry.MyEvent -= eh;
            }
        }

        async public Task processMessageAsync(string json, StreamWriter writer) {
            Logger.Debug("message received: {0}", json);
            var r = new SvcResponse();
            var rr = new ServiceStatusEvent();
            try {
                ActionEvent ae = serializer.Deserialize<ActionEvent>(new JsonTextReader(new StringReader(json)));
                Logger.Info("Op: {0}", ae.Op);
                switch (ae.Op.ToLower()) {
                    case "stop":
                        if (ae.Action == "Force") {
                            // attempt to forcefully find the process and terminate it...
                            Logger.Warn("User has requested a FORCEFUL termination of the service. It must be stuck. Current status: {0}", ServiceActions.ServiceStatus());
                            var procs = System.Diagnostics.Process.GetProcessesByName("ziti-tunnel");
                            if (procs == null || procs.Length == 0) {
                                Logger.Error("Process not found! Cannot terminate!");
                                rr.Code = -20;
                                rr.Error = "Process not found! Cannot terminate!";
                                rr.Message = "Could not terminate the service forcefully";
                                break;
                            }

                            foreach(var p in procs) {
                                Logger.Warn("Forcefully terminating process: {0}", p.Id);
                                p.Kill();
                            }
                            rr.Message = "Service has been terminated";
                            rr.Status = ServiceActions.ServiceStatus();
                            r = rr;
                        } else {
                            r.Message = ServiceActions.StopService();
                        }
                        break;
                    case "start":
                        r.Message = ServiceActions.StartService();
                        break;
                    case "status":
                        rr.Status = ServiceActions.ServiceStatus();
                        r = rr;
                        break;
                    case "capturelogs":
                        try {
                            string results = CaptureLogs();
                            r.Message = results;
                        } catch(Exception ex) {
                            string err = string.Format("UNKNOWN ERROR : {0}", ex.Message);
                            Logger.Error(ex, err);
                            r.Code = -5;
                            r.Message = "FAILURE";
                            r.Error = err;
                        }
                        break;
                    default:
                        r.Message = "FAILURE";
                        r.Code = -3;
                        r.Error = string.Format("UNKNOWN ACTION received: {0}", ae.Op);
                        Logger.Error(r.Message);
                        break;
                }
            } catch (Exception e) {
                Logger.Error(e, "Unexpected erorr in processMessage!");
                r.Message = "FAILURE";
                r.Code = -2;
                r.Error = e.Message + ":" + e?.InnerException?.Message;
            }
            Logger.Info("Returning status: {0}", r.Message);
            await writer.WriteLineAsync(JsonConvert.SerializeObject(r));
            await writer.FlushAsync();
        }
    }
}
