using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;

public class UpdateDLLsEditor
{
	private static readonly string geneticLibDllPath =
		"./Submodules/GeneticLib/GeneticLib/bin/Debug/netstandard2.0/GeneticLib.dll";
	private static readonly string geneticLibDLLDest =
		"./Assets/DLLs/GeneticLib.dll";

	[MenuItem("Tools/UpdateGeneticLibDLL")]
	private static void UpdateGeneticLib()
	{
		using (var proc = new Process())
		{
			var info = new ProcessStartInfo
			{
				FileName = "cp",
				Arguments = string.Format("{0} {1}", geneticLibDllPath, geneticLibDLLDest)
			};
			proc.StartInfo = info;
			var success = proc.Start();

			if (success)
			{
				UnityEngine.Debug.Log(string.Format("Successfully copied {0} to {1}",
				                                    geneticLibDllPath,
				                                    geneticLibDLLDest));
			}
			else
			{
				UnityEngine.Debug.LogError("Couldnt update genetics");
			}
		}
	}
}
