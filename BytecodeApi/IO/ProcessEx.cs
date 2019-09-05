﻿using BytecodeApi.Extensions;
using BytecodeApi.Threading;
using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace BytecodeApi.IO
{
	/// <summary>
	/// Provides <see langword="static" /> methods that extend the <see cref="Process" /> class.
	/// </summary>
	public static class ProcessEx
	{
		/// <summary>
		/// Gets a value indicating whether the current <see cref="Process" /> has a console window.
		/// </summary>
		public static bool HasConsole => Native.GetConsoleWindow() != IntPtr.Zero;

		/// <summary>
		/// Creates a new <see cref="Process" /> component for each process resource with the SessionId of the current <see cref="Process" /> on the local computer.
		/// </summary>
		/// <returns>
		/// An <see cref="Process" />[] that represents all the process resources with the SessionId of the current <see cref="Process" /> running on the local computer.
		/// </returns>
		public static Process[] GetSessionProcesses()
		{
			return Process
				.GetProcesses()
				.Where(process => process.SessionId == ApplicationBase.Process.SessionId)
				.ToArray();
		}
		/// <summary>
		/// Creates an array of new <see cref="Process" /> components and associates them with all the process resources on the local computer that share the specified process name and the SessionId of the current <see cref="Process" />.
		/// </summary>
		/// <param name="processName">A <see cref="string" /> specifying the friendly name of the process.</param>
		/// <returns>
		/// A <see cref="Process" />[] that represents the process resources running the specified application or file with the SessionId of the current <see cref="Process" />.
		/// </returns>
		public static Process[] GetSessionProcessesByName(string processName)
		{
			return Process
				.GetProcessesByName(processName)
				.Where(process => process.SessionId == ApplicationBase.Process.SessionId)
				.ToArray();
		}
		/// <summary>
		/// Creates a <see cref="Process" /> with the specified commandline and the specified <see cref="ProcessIntegrityLevel" />. If process creation fails, a <see cref="Win32Exception" /> is thrown. This is typically used to create processes with lower integrity.
		/// </summary>
		/// <param name="commandLine">A <see cref="string" /> specifying the commandline to create the <see cref="Process" /> with.</param>
		/// <param name="integrityLevel">The <see cref="ProcessIntegrityLevel" /> to create the <see cref="Process" /> with. This is usually lower than the <see cref="ProcessIntegrityLevel" /> of the current <see cref="Process" />.</param>
		/// <returns>
		/// The <see cref="Process" /> this method creates.
		/// </returns>
		public static Process StartWithIntegrity(string commandLine, ProcessIntegrityLevel integrityLevel)
		{
			Check.ArgumentNull(commandLine, nameof(commandLine));

			IntPtr token = IntPtr.Zero;
			IntPtr newToken = IntPtr.Zero;
			IntPtr integritySid = IntPtr.Zero;
			IntPtr tokenInfoPtr = IntPtr.Zero;
			Native.StartupInfo startupInfo = new Native.StartupInfo();
			Native.ProcessInformation processInformation = new Native.ProcessInformation();

			try
			{
				using (Process process = Process.GetCurrentProcess())
				{
					token = process.OpenToken(0x8b);
				}

				if (token != IntPtr.Zero && Native.DuplicateTokenEx(token, 0, IntPtr.Zero, 2, 1, out newToken))
				{
					Native.SidIdentifierAuthority securityMandatoryLabelAuthority = new Native.SidIdentifierAuthority { Value = new byte[] { 0, 0, 0, 0, 0, 16 } };
					if (Native.AllocateAndInitializeSid(ref securityMandatoryLabelAuthority, 1, (int)integrityLevel, 0, 0, 0, 0, 0, 0, 0, out integritySid))
					{
						Native.TokenMandatoryLabel mandatoryTokenLabel;
						mandatoryTokenLabel.Label.Attributes = 0x20;
						mandatoryTokenLabel.Label.Sid = integritySid;

						int tokenInfo = Marshal.SizeOf(mandatoryTokenLabel);
						tokenInfoPtr = Marshal.AllocHGlobal(tokenInfo);
						Marshal.StructureToPtr(mandatoryTokenLabel, tokenInfoPtr, false);
						if (Native.SetTokenInformation(newToken, 25, tokenInfoPtr, tokenInfo + Native.GetLengthSid(integritySid)))
						{
							startupInfo.StructSize = Marshal.SizeOf(startupInfo);
							if (Native.CreateProcessAsUser(newToken, null, commandLine, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref startupInfo, out processInformation))
							{
								return Process.GetProcessById(processInformation.ProcessId);
							}
						}
					}
				}
			}
			finally
			{
				if (token != IntPtr.Zero) Native.CloseHandle(token);
				if (newToken != IntPtr.Zero) Native.CloseHandle(newToken);
				if (integritySid != IntPtr.Zero) Native.FreeSid(integritySid);
				if (tokenInfoPtr != IntPtr.Zero) Marshal.FreeHGlobal(tokenInfoPtr);
				if (processInformation.Process != IntPtr.Zero) Native.CloseHandle(processInformation.Process);
				if (processInformation.Thread != IntPtr.Zero) Native.CloseHandle(processInformation.Thread);
			}

			throw Throw.Win32("Process could not be created.");
		}
		/// <summary>
		/// Creates a console window for the current <see cref="Process" />.
		/// </summary>
		/// <param name="alwaysCreateNewConsole"><see langword="true" /> to always create a new console window; <see langword="false" /> to attach to an existing console window, if one already exists.</param>
		/// <param name="setInStream"><see langword="true" /> to set the input stream.</param>
		public static void CreateConsole(bool alwaysCreateNewConsole, bool setInStream)
		{
			//TODO: Bug: Does not support console color; setInStream causes to pause the application
			bool consoleAttached = true;
			if (alwaysCreateNewConsole || Native.AttachConsole(0xffffffff) == 0 && Marshal.GetLastWin32Error() != 5)
			{
				consoleAttached = Native.AllocConsole() != 0;
			}

			if (consoleAttached)
			{
				FileStream outStream = CreateFileStream("CONOUT$", 0x40000000, FileShare.Write, FileAccess.Write);
				if (outStream != null)
				{
					StreamWriter streamWriter = new StreamWriter(outStream) { AutoFlush = true };
					Console.SetOut(streamWriter);
					Console.SetError(streamWriter);
				}

				if (setInStream)
				{
					FileStream inStream = CreateFileStream("CONIN$", 0x80000000, FileShare.Read, FileAccess.Read);
					if (inStream != null) Console.SetIn(new StreamReader(inStream));
				}
			}

			FileStream CreateFileStream(string name, uint access, FileShare fileShare, FileAccess fileAccess)
			{
				SafeFileHandle file = Native.CreateFile(name, access, fileShare, IntPtr.Zero, FileMode.Open, 0x80, IntPtr.Zero);
				return file.IsInvalid ? null : new FileStream(file, fileAccess);
			}
		}
		/// <summary>
		/// Creates a <see cref="Process" />, reads the standard output stream and waits until the process has exited.
		/// </summary>
		/// <param name="fileName">The name of an application file to run in the process.</param>
		/// <returns>
		/// The result <see cref="string" /> from the standard output stream of the process after it has exited.
		/// </returns>
		public static string ReadProcessOutput(string fileName)
		{
			return ReadProcessOutput(fileName, null);
		}
		/// <summary>
		/// Creates a <see cref="Process" />, reads the standard output stream and waits until the process has exited.
		/// </summary>
		/// <param name="fileName">The name of an application file to run in the process.</param>
		/// <param name="arguments">Command-line arguments to pass when starting the process.</param>
		/// <returns>
		/// The result <see cref="string" /> from the standard output stream of the process after it has exited.
		/// </returns>
		public static string ReadProcessOutput(string fileName, string arguments)
		{
			return ReadProcessOutput(fileName, arguments, false);
		}
		/// <summary>
		/// Creates a <see cref="Process" />, reads the standard output stream and waits until the process has exited.
		/// </summary>
		/// <param name="fileName">The name of an application file to run in the process.</param>
		/// <param name="arguments">Command-line arguments to pass when starting the process.</param>
		/// <param name="inclueErrorStream"><see langword="true" /> to include the standard error stream; <see langword="false" /> to exclude it.</param>
		/// <returns>
		/// The result <see cref="string" /> from the standard output stream of the process after it has exited.
		/// </returns>
		public static string ReadProcessOutput(string fileName, string arguments, bool inclueErrorStream)
		{
			Check.ArgumentNull(fileName, nameof(fileName));

			StringBuilder result = new StringBuilder();

			using (Process process = new Process
			{
				StartInfo =
				{
					FileName = fileName,
					Arguments = arguments,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				},
				EnableRaisingEvents = true
			})
			{
				process.OutputDataReceived += DataReceived;
				if (inclueErrorStream) process.ErrorDataReceived += DataReceived;

				process.Start();
				process.BeginOutputReadLine();
				if (inclueErrorStream) process.BeginErrorReadLine();
				process.WaitForExit();
			}

			return result.ToString();

			void DataReceived(object sender, DataReceivedEventArgs e)
			{
				result.AppendLine(e.Data);
			}
		}

		/// <summary>
		/// Executes a .NET executable from a <see cref="byte" />[] by invoking the main entry point. The Main method must either have no parameters or one <see cref="string" />[] parameter. If it has a parameter, <see langword="new" /> <see cref="string" />[0] is passed.
		/// </summary>
		/// <param name="executable">A <see cref="byte" />[] that represents a .NET executable file.</param>
		public static void ExecuteDotNetAssembly(byte[] executable)
		{
			ExecuteDotNetAssembly(executable, null);
		}
		/// <summary>
		/// Executes a .NET executable from a <see cref="byte" />[] by invoking the main entry point. The Main method must either have no parameters or one <see cref="string" />[] parameter. If it has a parameter, <paramref name="args" /> is passed, otherwise <paramref name="args" /> is ignored.
		/// </summary>
		/// <param name="executable">A <see cref="byte" />[] that represents a .NET executable file.</param>
		/// <param name="args">A <see cref="string" />[] representing the arguments that is passed to the main entry point, if the Main method has a <see cref="string" />[] parameter.</param>
		public static void ExecuteDotNetAssembly(byte[] executable, params string[] args)
		{

			ExecuteDotNetAssembly(executable, args, false);
		}
		/// <summary>
		/// Executes a .NET executable from a <see cref="byte" />[] by invoking the main entry point. The Main method must either have no parameters or one <see cref="string" />[] parameter. If it has a parameter, <paramref name="args" /> is passed, otherwise <paramref name="args" /> is ignored.
		/// </summary>
		/// <param name="executable">A <see cref="byte" />[] that represents a .NET executable file.</param>
		/// <param name="args">A <see cref="string" />[] representing the arguments that is passed to the main entry point, if the Main method has a <see cref="string" />[] parameter.</param>
		/// <param name="thread"><see langword="true" /> to invoke the main entry point in a new thread.</param>
		public static void ExecuteDotNetAssembly(byte[] executable, string[] args, bool thread)
		{
			Check.ArgumentNull(executable, nameof(executable));

			MethodInfo method = Assembly.Load(executable).EntryPoint;
			ParameterInfo[] parameters = method.GetParameters();

			Action invoke;

			if (parameters.Length == 0) invoke = () => method.Invoke();
			else if (parameters.Length == 1 && parameters.First().ParameterType == typeof(string[])) invoke = () => method.Invoke(null, SingletonCollection.Array(args ?? new string[0]));
			else throw Throw.InvalidOperation("Executable does not contain a static 'main' method suitable for an entry point.");

			if (thread) ThreadFactory.StartThread(invoke);
			else invoke();
		}
		/// <summary>
		/// Detects process analysers, such as sandboxes, virtual environments, or specific debuggers or profilers.
		/// </summary>
		/// <param name="processAnalyser">The <see cref="ProcessAnalyser" /> to test.</param>
		/// <returns>
		/// <see langword="true" />, if the specified process analyser has been detected;
		/// otherwise, <see langword="false" />.
		/// </returns>
		public static bool DetectProcessAnalyser(ProcessAnalyser processAnalyser)
		{
			//FEATURE: Sandboxes, virtual machines, CheatEngine (https://www.aspfree.com/c/a/braindump/virtualization-and-sandbox-detection/)		
			if (processAnalyser == ProcessAnalyser.Sandboxie)
			{
				return Native.GetModuleHandle("SbieDll") != IntPtr.Zero;
			}
			else if (processAnalyser == ProcessAnalyser.Emulator)
			{
				int start = Environment.TickCount;
				Stopwatch stopwatch = ThreadFactory.StartStopwatch();
				Thread.Sleep(500);
				int stop = Environment.TickCount;
				return stop - start < 450 || stopwatch.Elapsed < TimeSpan.FromMilliseconds(450);
			}
			else if (processAnalyser == ProcessAnalyser.Wireshark)
			{
				return Process.GetProcessesByName("Wireshark").Any() || Process.GetProcesses().Any(process => CSharp.Try(() => process?.MainWindowTitle).Equals("The Wireshark Network Analyzer", SpecialStringComparisons.NullAndEmptyEqual | SpecialStringComparisons.IgnoreCase));
			}
			else if (processAnalyser == ProcessAnalyser.ProcessMonitor)
			{
				return Process.GetProcesses().Any(process => CSharp.Try(() => process?.MainWindowTitle).Contains("Process Monitor -", SpecialStringComparisons.IgnoreCase));
			}
			else
			{
				throw Throw.InvalidEnumArgument(nameof(processAnalyser), processAnalyser);
			}
		}
		/// <summary>
		/// If the current <see cref="Process" /> is running with elevated privileges, a blue screen is triggered and the operating system is terminated; otherwise, an exception is thrown.
		/// </summary>
		public static void BlueScreen()
		{
			int processInformation = 1;
			if (Native.NtSetInformationProcess(Process.GetCurrentProcess().Handle, 29, ref processInformation, 4) == 0)
			{
				Environment.Exit(0);
			}

			throw Throw.InvalidOperation("Could not trigger a blue screen.");
		}
	}
}