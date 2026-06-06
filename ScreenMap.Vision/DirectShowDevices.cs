#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ScreenMap.Vision;

/// <summary>
/// Reads camera friendly names from the DirectShow video-input device category.
/// OpenCV's DSHOW backend enumerates devices in the same order as the system device
/// enumerator, so the position in this list matches the OpenCV device index.
/// </summary>
public static class DirectShowDevices
{
    private static readonly Guid CLSID_SystemDeviceEnum =
        new("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
    private static readonly Guid CLSID_VideoInputDeviceCategory =
        new("860BB310-5D01-11d0-BD3B-00A0C911CE86");

    /// <summary>
    /// Friendly names of video-input devices, indexed to match OpenCV's DSHOW device index.
    /// Returns an empty list if DirectShow enumeration fails (e.g. on non-Windows).
    /// </summary>
    public static IReadOnlyList<string> GetNames()
    {
        var names = new List<string>();
        object? devEnumObj = null;
        try
        {
            var type = Type.GetTypeFromCLSID(CLSID_SystemDeviceEnum);
            if (type == null) return names;
            devEnumObj = Activator.CreateInstance(type);
            if (devEnumObj is not ICreateDevEnum devEnum) return names;

            var category = CLSID_VideoInputDeviceCategory;
            int hr = devEnum.CreateClassEnumerator(ref category, out IEnumMoniker? enumMoniker, 0);
            // hr == 1 (S_FALSE) means the category is empty.
            if (hr != 0 || enumMoniker == null) return names;

            try
            {
                var monikers = new IMoniker[1];
                while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    var moniker = monikers[0];
                    names.Add(ReadFriendlyName(moniker));
                    Marshal.ReleaseComObject(moniker);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumMoniker);
            }
        }
        catch
        {
            // DirectShow unavailable: caller falls back to numeric indices.
        }
        finally
        {
            if (devEnumObj != null) Marshal.ReleaseComObject(devEnumObj);
        }
        return names;
    }

    private static string ReadFriendlyName(IMoniker moniker)
    {
        object? bagObj = null;
        try
        {
            Guid bagId = typeof(IPropertyBag).GUID;
            moniker.BindToStorage(null!, null!, ref bagId, out bagObj);
            if (bagObj is IPropertyBag bag &&
                bag.Read("FriendlyName", out object? value, IntPtr.Zero) == 0 &&
                value is string name)
            {
                return name;
            }
        }
        catch
        {
            // Fall through to placeholder.
        }
        finally
        {
            if (bagObj != null) Marshal.ReleaseComObject(bagObj);
        }
        return "Unknown camera";
    }

    [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateDevEnum
    {
        [PreserveSig]
        int CreateClassEnumerator(ref Guid clsidDeviceClass, out IEnumMoniker? enumMoniker, int flags);
    }

    [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyBag
    {
        [PreserveSig]
        int Read([MarshalAs(UnmanagedType.LPWStr)] string propName,
                 [MarshalAs(UnmanagedType.Struct)] out object? value, IntPtr errorLog);
        [PreserveSig]
        int Write([MarshalAs(UnmanagedType.LPWStr)] string propName,
                  [MarshalAs(UnmanagedType.Struct)] ref object value);
    }
}
