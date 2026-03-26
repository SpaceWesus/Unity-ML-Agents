using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct PointCloudInstanceData
{
	public Vector3 relativePosition;
	public uint color;
};
