// COPYRIGHT 1995-2025 ESRI
// TRADE SECRETS: ESRI PROPRIETARY AND CONFIDENTIAL
// Unpublished material - all rights reserved under the
// Copyright Laws of the United States and applicable international
// laws, treaties, and conventions.
//
// For additional information, contact:
// Attn: Contracts and Legal Department
// Environmental Systems Research Institute, Inc.
// 380 New York Street
// Redlands, California 92373
// USA
//
// email: legal@esri.com
using System.Runtime.InteropServices;
using System;

namespace Esri.Unity
{
    [StructLayout(LayoutKind.Sequential)]
    public class ArcGISCollection<T>
    {
        #region Constructors
        public ArcGISCollection()
        {
            if (typeof (T) == typeof(GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter))
            {
                var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection();
                
                Handle = collection.Handle;
                
                collection.Handle = IntPtr.Zero;
            }
            else if (typeof (T) == typeof(GameEngine.Elevation.Base.ArcGISElevationSource))
            {
                var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection();
                
                Handle = collection.Handle;
                
                collection.Handle = IntPtr.Zero;
            }
            else if (typeof (T) == typeof(GameEngine.Layers.Base.ArcGISLayer))
            {
                var collection = new GameEngine.Layers.Base.ArcGISLayerCollection();
                
                Handle = collection.Handle;
                
                collection.Handle = IntPtr.Zero;
            }
            else if (typeof (T) == typeof(GameEngine.Layers.ArcGISMeshModification))
            {
                var collection = new GameEngine.Layers.ArcGISMeshModificationCollection();
                
                Handle = collection.Handle;
                
                collection.Handle = IntPtr.Zero;
            }
            else if (typeof (T) == typeof(GameEngine.Geometry.ArcGISPolygon))
            {
                var collection = new GameEngine.Geometry.ArcGISPolygonCollection();
                
                Handle = collection.Handle;
                
                collection.Handle = IntPtr.Zero;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        #endregion // Constructors
    
        #region Internal Members
        internal ArcGISCollection(IntPtr handle) => Handle = handle;
    
        ~ArcGISCollection()
        {
            if (typeof (T) == typeof(GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter))
            {
                new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(Handle);
            }
            else if (typeof (T) == typeof(GameEngine.Elevation.Base.ArcGISElevationSource))
            {
                new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(Handle);
            }
            else if (typeof (T) == typeof(GameEngine.Layers.Base.ArcGISLayer))
            {
                new GameEngine.Layers.Base.ArcGISLayerCollection(Handle);
            }
            else if (typeof (T) == typeof(GameEngine.Layers.ArcGISMeshModification))
            {
                new GameEngine.Layers.ArcGISMeshModificationCollection(Handle);
            }
            else if (typeof (T) == typeof(GameEngine.Geometry.ArcGISPolygon))
            {
                new GameEngine.Geometry.ArcGISPolygonCollection(Handle);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    
        internal IntPtr Handle { get; private set; }
        #endregion // Internal Members
    }
    
    public static class ArcGISCollectionSpecialization
    {
        public static ulong Add(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self, GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter value)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	var result = collection.Add(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong AddArray(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self, ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> vector2)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	var result = collection.AddArray(vector2);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter At(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self, ulong position)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	var result = collection.At(position);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static bool Contains(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self, GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter value)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	var result = collection.Contains(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static bool Equals(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self, ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> vector2)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	var result = collection.Equals(vector2);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter First(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	var result = collection.First();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong GetSize(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	var result = collection.Size;
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong IndexOf(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self, GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter value)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	var result = collection.IndexOf(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static void Insert(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self, ulong position, GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter value)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	collection.Insert(position, value);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static bool IsEmpty(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	var result = collection.IsEmpty();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter Last(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	var result = collection.Last();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static void Move(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self, ulong oldPosition, ulong newPosition)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	collection.Move(oldPosition, newPosition);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static ulong Npos(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self)
        {
        	return GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection.Npos();
        }
        
        public static void Remove(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self, ulong position)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	collection.Remove(position);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static void RemoveAll(this ArcGISCollection<GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilter> self)
        {
        	var collection = new GameEngine.Layers.BuildingScene.ArcGISBuildingAttributeFilterCollection(self.Handle);
        
        	collection.RemoveAll();
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static ulong Add(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self, GameEngine.Elevation.Base.ArcGISElevationSource value)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	var result = collection.Add(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong AddArray(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self, ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> vector2)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	var result = collection.AddArray(vector2);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Elevation.Base.ArcGISElevationSource At(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self, ulong position)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	var result = collection.At(position);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static bool Contains(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self, GameEngine.Elevation.Base.ArcGISElevationSource value)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	var result = collection.Contains(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static bool Equals(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self, ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> vector2)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	var result = collection.Equals(vector2);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Elevation.Base.ArcGISElevationSource First(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	var result = collection.First();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong GetSize(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	var result = collection.Size;
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong IndexOf(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self, GameEngine.Elevation.Base.ArcGISElevationSource value)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	var result = collection.IndexOf(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static void Insert(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self, ulong position, GameEngine.Elevation.Base.ArcGISElevationSource value)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	collection.Insert(position, value);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static bool IsEmpty(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	var result = collection.IsEmpty();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Elevation.Base.ArcGISElevationSource Last(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	var result = collection.Last();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static void Move(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self, ulong oldPosition, ulong newPosition)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	collection.Move(oldPosition, newPosition);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static ulong Npos(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self)
        {
        	return GameEngine.Elevation.Base.ArcGISElevationSourceCollection.Npos();
        }
        
        public static void Remove(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self, ulong position)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	collection.Remove(position);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static void RemoveAll(this ArcGISCollection<GameEngine.Elevation.Base.ArcGISElevationSource> self)
        {
        	var collection = new GameEngine.Elevation.Base.ArcGISElevationSourceCollection(self.Handle);
        
        	collection.RemoveAll();
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static ulong Add(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self, GameEngine.Layers.Base.ArcGISLayer value)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	var result = collection.Add(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong AddArray(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self, ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> vector2)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	var result = collection.AddArray(vector2);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Layers.Base.ArcGISLayer At(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self, ulong position)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	var result = collection.At(position);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static bool Contains(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self, GameEngine.Layers.Base.ArcGISLayer value)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	var result = collection.Contains(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static bool Equals(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self, ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> vector2)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	var result = collection.Equals(vector2);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Layers.Base.ArcGISLayer First(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	var result = collection.First();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong GetSize(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	var result = collection.Size;
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong IndexOf(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self, GameEngine.Layers.Base.ArcGISLayer value)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	var result = collection.IndexOf(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static void Insert(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self, ulong position, GameEngine.Layers.Base.ArcGISLayer value)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	collection.Insert(position, value);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static bool IsEmpty(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	var result = collection.IsEmpty();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Layers.Base.ArcGISLayer Last(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	var result = collection.Last();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static void Move(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self, ulong oldPosition, ulong newPosition)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	collection.Move(oldPosition, newPosition);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static ulong Npos(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self)
        {
        	return GameEngine.Layers.Base.ArcGISLayerCollection.Npos();
        }
        
        public static void Remove(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self, ulong position)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	collection.Remove(position);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static void RemoveAll(this ArcGISCollection<GameEngine.Layers.Base.ArcGISLayer> self)
        {
        	var collection = new GameEngine.Layers.Base.ArcGISLayerCollection(self.Handle);
        
        	collection.RemoveAll();
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static ulong Add(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self, GameEngine.Layers.ArcGISMeshModification value)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	var result = collection.Add(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong AddArray(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self, ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> vector2)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	var result = collection.AddArray(vector2);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Layers.ArcGISMeshModification At(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self, ulong position)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	var result = collection.At(position);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static bool Contains(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self, GameEngine.Layers.ArcGISMeshModification value)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	var result = collection.Contains(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static bool Equals(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self, ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> vector2)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	var result = collection.Equals(vector2);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Layers.ArcGISMeshModification First(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	var result = collection.First();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong GetSize(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	var result = collection.Size;
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong IndexOf(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self, GameEngine.Layers.ArcGISMeshModification value)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	var result = collection.IndexOf(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static void Insert(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self, ulong position, GameEngine.Layers.ArcGISMeshModification value)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	collection.Insert(position, value);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static bool IsEmpty(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	var result = collection.IsEmpty();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Layers.ArcGISMeshModification Last(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	var result = collection.Last();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static void Move(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self, ulong oldPosition, ulong newPosition)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	collection.Move(oldPosition, newPosition);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static ulong Npos(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self)
        {
        	return GameEngine.Layers.ArcGISMeshModificationCollection.Npos();
        }
        
        public static void Remove(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self, ulong position)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	collection.Remove(position);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static void RemoveAll(this ArcGISCollection<GameEngine.Layers.ArcGISMeshModification> self)
        {
        	var collection = new GameEngine.Layers.ArcGISMeshModificationCollection(self.Handle);
        
        	collection.RemoveAll();
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static ulong Add(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self, GameEngine.Geometry.ArcGISPolygon value)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	var result = collection.Add(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong AddArray(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self, ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> vector2)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	var result = collection.AddArray(vector2);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Geometry.ArcGISPolygon At(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self, ulong position)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	var result = collection.At(position);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static bool Contains(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self, GameEngine.Geometry.ArcGISPolygon value)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	var result = collection.Contains(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static bool Equals(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self, ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> vector2)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	var result = collection.Equals(vector2);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Geometry.ArcGISPolygon First(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	var result = collection.First();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong GetSize(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	var result = collection.Size;
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static ulong IndexOf(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self, GameEngine.Geometry.ArcGISPolygon value)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	var result = collection.IndexOf(value);
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static void Insert(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self, ulong position, GameEngine.Geometry.ArcGISPolygon value)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	collection.Insert(position, value);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static bool IsEmpty(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	var result = collection.IsEmpty();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static GameEngine.Geometry.ArcGISPolygon Last(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	var result = collection.Last();
        
        	collection.Handle = IntPtr.Zero;
        
        	return result;
        }
        
        public static void Move(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self, ulong oldPosition, ulong newPosition)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	collection.Move(oldPosition, newPosition);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static ulong Npos(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self)
        {
        	return GameEngine.Geometry.ArcGISPolygonCollection.Npos();
        }
        
        public static void Remove(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self, ulong position)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	collection.Remove(position);
        
        	collection.Handle = IntPtr.Zero;
        }
        
        public static void RemoveAll(this ArcGISCollection<GameEngine.Geometry.ArcGISPolygon> self)
        {
        	var collection = new GameEngine.Geometry.ArcGISPolygonCollection(self.Handle);
        
        	collection.RemoveAll();
        
        	collection.Handle = IntPtr.Zero;
        }
    }
}