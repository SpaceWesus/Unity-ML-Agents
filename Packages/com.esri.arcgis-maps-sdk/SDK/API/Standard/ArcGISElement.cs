// COPYRIGHT 1995-2020 ESRI
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

namespace Esri.Standard
{
    /// <summary>
    /// Defines an element that can be added to a container object.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal partial class ArcGISElement
    {
        #region Properties
        /// <summary>
        /// The type that the element is holding.
        /// </summary>
        internal ArcGISElementType ObjectType
        {
            get
            {
                var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
                
                var localResult = PInvoke.RT_Element_getObjectType(Handle, errorHandler);
                
                Unity.ArcGISErrorManager.CheckError(errorHandler);
                
                return localResult;
            }
        }
        #endregion // Properties
        
        #region Methods
        /// <summary>
        /// Creates an element from an <see cref="GameEngine.Authentication.ArcGISCredential">ArcGISCredential</see> instance.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromArcGISCredential(GameEngine.Authentication.ArcGISCredential value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

            var localValue = value.Handle;

            var localResult = PInvoke.RT_Element_fromArcGISCredential(localValue, errorHandler);

            Unity.ArcGISErrorManager.CheckError(errorHandler);

            ArcGISElement localLocalResult = null;

            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new ArcGISElement(localResult);
            }

            return localLocalResult;
        }

        /// <summary>
        /// Creates an element from an <see cref="GameEngine.Authentication.ArcGISTokenInfo">ArcGISTokenInfo</see> instance.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromArcGISTokenInfo(GameEngine.Authentication.ArcGISTokenInfo value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

            var localValue = value.Handle;

            var localResult = PInvoke.RT_Element_fromArcGISTokenInfo(localValue, errorHandler);

            Unity.ArcGISErrorManager.CheckError(errorHandler);

            ArcGISElement localLocalResult = null;

            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new ArcGISElement(localResult);
            }

            return localLocalResult;
        }

        /// <summary>
        /// Creates an element from a <see cref="GameEngine.Geometry.ArcGISDatumTransformation">ArcGISDatumTransformation</see> instance.
        /// </summary>
        /// <param name="value">The instance.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromDatumTransformation(GameEngine.Geometry.ArcGISDatumTransformation value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            var localResult = PInvoke.RT_Element_fromDatumTransformation(localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Creates an element from a dictionary value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromDictionary(Unity.ArcGISDictionaryBase value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            var localResult = PInvoke.RT_Element_fromDictionary(localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Creates an element from a float64 value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromFloat64(double value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_fromFloat64(value, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Creates an element object from an attribute.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromGEAttribute(GameEngine.Attributes.ArcGISAttribute value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            var localResult = PInvoke.RT_Element_fromGEAttribute(localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Creates an element from a <see cref="GameEngine.Layers.BuildingScene.ArcGISBuildingSceneLayerAttributeStatistics">ArcGISBuildingSceneLayerAttributeStatistics</see>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromGEBuildingSceneLayerAttributeStatistics(GameEngine.Layers.BuildingScene.ArcGISBuildingSceneLayerAttributeStatistics value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            var localResult = PInvoke.RT_Element_fromGEBuildingSceneLayerAttributeStatistics(localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Creates an element from a <see cref="GameEngine.Geometry.ArcGISGeographicTransformationStep">ArcGISGeographicTransformationStep</see> instance.
        /// </summary>
        /// <param name="value">The instance.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromGeographicTransformationStep(GameEngine.Geometry.ArcGISGeographicTransformationStep value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            var localResult = PInvoke.RT_Element_fromGeographicTransformationStep(localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Creates an element from a geometry value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>
        /// </returns>
        internal static ArcGISElement FromGeometry(GameEngine.Geometry.ArcGISGeometry value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            var localResult = PInvoke.RT_Element_fromGeometry(localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Creates an element object from a visualization attribute.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromGEVisualizationAttribute(GameEngine.Attributes.ArcGISVisualizationAttribute value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            var localResult = PInvoke.RT_Element_fromGEVisualizationAttribute(localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Creates an element object from a visualization attribute description.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromGEVisualizationAttributeDescription(GameEngine.Attributes.ArcGISVisualizationAttributeDescription value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            var localResult = PInvoke.RT_Element_fromGEVisualizationAttributeDescription(localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Creates an element from a <see cref="GameEngine.Geometry.ArcGISHorizontalVerticalTransformationStep">ArcGISHorizontalVerticalTransformationStep</see> instance.
        /// </summary>
        /// <param name="value">The instance.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromHorizontalVerticalTransformationStep(GameEngine.Geometry.ArcGISHorizontalVerticalTransformationStep value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            var localResult = PInvoke.RT_Element_fromHorizontalVerticalTransformationStep(localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }

        /// <summary>
        /// Creates an element from an <see cref="GameEngine.Authentication.ArcGISOAuthUserCredential">ArcGISOAuthUserCredential</see> instance.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromOAuthUserCredential(GameEngine.Authentication.ArcGISOAuthUserCredential value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

            var localValue = value.Handle;

            var localResult = PInvoke.RT_Element_fromOAuthUserCredential(localValue, errorHandler);

            Unity.ArcGISErrorManager.CheckError(errorHandler);

            ArcGISElement localLocalResult = null;

            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new ArcGISElement(localResult);
            }

            return localLocalResult;
        }

        /// <summary>
        /// Creates an element from an <see cref="GameEngine.Authentication.ArcGISOAuthUserTokenInfo">ArcGISOAuthUserTokenInfo</see> instance.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromOAuthUserTokenInfo(GameEngine.Authentication.ArcGISOAuthUserTokenInfo value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

            var localValue = value.Handle;

            var localResult = PInvoke.RT_Element_fromOAuthUserTokenInfo(localValue, errorHandler);

            Unity.ArcGISErrorManager.CheckError(errorHandler);

            ArcGISElement localLocalResult = null;

            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new ArcGISElement(localResult);
            }

            return localLocalResult;
        }

        /// <summary>
        /// Creates an element from a string value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromString(string value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_fromString(value, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Creates an element from a uint32_t value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// An <see cref="Standard.ArcGISElement">ArcGISElement</see>.
        /// </returns>
        internal static ArcGISElement FromUInt32(uint value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_fromUInt32(value, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISElement localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    default:
                        localLocalResult = new ArcGISElement(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }

        /// <summary>
        /// Gets the value of an element as an <see cref="GameEngine.Authentication.ArcGISCredential">ArcGISCredential</see>..
        /// </summary>
        /// <returns>
        /// An <see cref="GameEngine.Authentication.ArcGISCredential">ArcGISCredential</see>.
        /// </returns>
        internal GameEngine.Authentication.ArcGISCredential GetValueAsArcGISCredential()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

            var localResult = PInvoke.RT_Element_getValueAsArcGISCredential(Handle, errorHandler);

            Unity.ArcGISErrorManager.CheckError(errorHandler);

            GameEngine.Authentication.ArcGISCredential localLocalResult = null;

            if (localResult != IntPtr.Zero)
            {
                var objectType = Standard.PInvoke.RT_Element_getObjectType(localResult, IntPtr.Zero);

                switch (objectType)
                {
                    case ArcGISElementType.OAuthUserCredential:
                        localLocalResult = new GameEngine.Authentication.ArcGISOAuthUserCredential(localResult);
                        break;
                    default:
                        localLocalResult = new GameEngine.Authentication.ArcGISCredential(localResult);
                        break;
                }
            }

            return localLocalResult;
        }

        /// <summary>
        /// Gets the value of an element as an <see cref="GameEngine.Authentication.ArcGISTokenInfo">ArcGISTokenInfo</see>.
        /// </summary>
        /// <returns>
        /// An <see cref="GameEngine.Authentication.ArcGISTokenInfo">ArcGISTokenInfo</see>.
        /// </returns>
        internal GameEngine.Authentication.ArcGISTokenInfo GetValueAsArcGISTokenInfo()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

            var localResult = PInvoke.RT_Element_getValueAsArcGISTokenInfo(Handle, errorHandler);

            Unity.ArcGISErrorManager.CheckError(errorHandler);

            GameEngine.Authentication.ArcGISTokenInfo localLocalResult = null;

            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new GameEngine.Authentication.ArcGISTokenInfo(localResult);
            }

            return localLocalResult;
        }

        /// <summary>
        /// Gets the value of the element as an <see cref="GameEngine.Geometry.ArcGISDatumTransformation">ArcGISDatumTransformation</see>.
        /// </summary>
        /// <returns>
        /// An <see cref="GameEngine.Geometry.ArcGISDatumTransformation">ArcGISDatumTransformation</see>.
        /// </returns>
        internal GameEngine.Geometry.ArcGISDatumTransformation GetValueAsDatumTransformation()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsDatumTransformation(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            GameEngine.Geometry.ArcGISDatumTransformation localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = GameEngine.Geometry.PInvoke.RT_DatumTransformation_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    case GameEngine.Geometry.ArcGISDatumTransformationType.GeographicTransformation:
                        localLocalResult = new GameEngine.Geometry.ArcGISGeographicTransformation(localResult);
                        break;
                    case GameEngine.Geometry.ArcGISDatumTransformationType.HorizontalVerticalTransformation:
                        localLocalResult = new GameEngine.Geometry.ArcGISHorizontalVerticalTransformation(localResult);
                        break;
                    default:
                        localLocalResult = new GameEngine.Geometry.ArcGISDatumTransformation(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Gets the element value as a dictionary.
        /// </summary>
        /// <returns>
        /// A <see cref="Unity.ArcGISDictionary<object, object>">ArcGISDictionary<object, object></see>.
        /// </returns>
        internal Unity.ArcGISDictionaryBase GetValueAsDictionary()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsDictionary(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            Unity.ArcGISDictionaryBase localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new Unity.ArcGISDictionaryBase(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Gets the value of the element.
        /// </summary>
        /// <returns>
        /// An float64.
        /// </returns>
        internal double GetValueAsFloat64()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsFloat64(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            return localResult;
        }
        
        /// <summary>
        /// Gets the value of an element as an attribute.
        /// </summary>
        /// <returns>
        /// A <see cref="GameEngine.Attributes.ArcGISAttribute">ArcGISAttribute</see>.
        /// </returns>
        internal GameEngine.Attributes.ArcGISAttribute GetValueAsGEAttribute()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsGEAttribute(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            GameEngine.Attributes.ArcGISAttribute localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new GameEngine.Attributes.ArcGISAttribute(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Gets the value of an element as a <see cref="GameEngine.Layers.BuildingScene.ArcGISBuildingSceneLayerAttributeStatistics">ArcGISBuildingSceneLayerAttributeStatistics</see>.
        /// </summary>
        /// <returns>
        /// a <see cref="GameEngine.Layers.BuildingScene.ArcGISBuildingSceneLayerAttributeStatistics">ArcGISBuildingSceneLayerAttributeStatistics</see>.
        /// </returns>
        internal GameEngine.Layers.BuildingScene.ArcGISBuildingSceneLayerAttributeStatistics GetValueAsGEBuildingSceneLayerAttributeStatistics()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsGEBuildingSceneLayerAttributeStatistics(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            GameEngine.Layers.BuildingScene.ArcGISBuildingSceneLayerAttributeStatistics localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new GameEngine.Layers.BuildingScene.ArcGISBuildingSceneLayerAttributeStatistics(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Gets the value of the element as an <see cref="GameEngine.Geometry.ArcGISGeographicTransformationStep">ArcGISGeographicTransformationStep</see>.
        /// </summary>
        /// <returns>
        /// An <see cref="GameEngine.Geometry.ArcGISGeographicTransformationStep">ArcGISGeographicTransformationStep</see>.
        /// </returns>
        internal GameEngine.Geometry.ArcGISGeographicTransformationStep GetValueAsGeographicTransformationStep()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsGeographicTransformationStep(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            GameEngine.Geometry.ArcGISGeographicTransformationStep localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new GameEngine.Geometry.ArcGISGeographicTransformationStep(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Gets the value of the element.
        /// </summary>
        /// <returns>
        /// An <see cref="GameEngine.Geometry.ArcGISGeometry">ArcGISGeometry</see>.
        /// </returns>
        internal GameEngine.Geometry.ArcGISGeometry GetValueAsGeometry()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsGeometry(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            GameEngine.Geometry.ArcGISGeometry localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                var objectType = GameEngine.Geometry.PInvoke.RT_Geometry_getObjectType(localResult, IntPtr.Zero);
                
                switch (objectType)
                {
                    case GameEngine.Geometry.ArcGISGeometryType.Envelope:
                        localLocalResult = new GameEngine.Geometry.ArcGISEnvelope(localResult);
                        break;
                    case GameEngine.Geometry.ArcGISGeometryType.Multipoint:
                        localLocalResult = new GameEngine.Geometry.ArcGISMultipoint(localResult);
                        break;
                    case GameEngine.Geometry.ArcGISGeometryType.Point:
                        localLocalResult = new GameEngine.Geometry.ArcGISPoint(localResult);
                        break;
                    case GameEngine.Geometry.ArcGISGeometryType.Polygon:
                        localLocalResult = new GameEngine.Geometry.ArcGISPolygon(localResult);
                        break;
                    case GameEngine.Geometry.ArcGISGeometryType.Polyline:
                        localLocalResult = new GameEngine.Geometry.ArcGISPolyline(localResult);
                        break;
                    default:
                        localLocalResult = new GameEngine.Geometry.ArcGISGeometry(localResult);
                        break;
                }
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Gets the value of an element as a visualization attribute.
        /// </summary>
        /// <returns>
        /// A <see cref="GameEngine.Attributes.ArcGISVisualizationAttribute">ArcGISVisualizationAttribute</see>.
        /// </returns>
        internal GameEngine.Attributes.ArcGISVisualizationAttribute GetValueAsGEVisualizationAttribute()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsGEVisualizationAttribute(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            GameEngine.Attributes.ArcGISVisualizationAttribute localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new GameEngine.Attributes.ArcGISVisualizationAttribute(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Gets the value of an element as a visualization attribute description.
        /// </summary>
        /// <returns>
        /// A <see cref="GameEngine.Attributes.ArcGISVisualizationAttributeDescription">ArcGISVisualizationAttributeDescription</see>.
        /// </returns>
        internal GameEngine.Attributes.ArcGISVisualizationAttributeDescription GetValueAsGEVisualizationAttributeDescription()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsGEVisualizationAttributeDescription(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            GameEngine.Attributes.ArcGISVisualizationAttributeDescription localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new GameEngine.Attributes.ArcGISVisualizationAttributeDescription(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Gets the value of the element as an <see cref="GameEngine.Geometry.ArcGISHorizontalVerticalTransformationStep">ArcGISHorizontalVerticalTransformationStep</see>.
        /// </summary>
        /// <returns>
        /// An <see cref="GameEngine.Geometry.ArcGISHorizontalVerticalTransformationStep">ArcGISHorizontalVerticalTransformationStep</see>.
        /// </returns>
        internal GameEngine.Geometry.ArcGISHorizontalVerticalTransformationStep GetValueAsHorizontalVerticalTransformationStep()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsHorizontalVerticalTransformationStep(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            GameEngine.Geometry.ArcGISHorizontalVerticalTransformationStep localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new GameEngine.Geometry.ArcGISHorizontalVerticalTransformationStep(localResult);
            }
            
            return localLocalResult;
        }

        /// <summary>
        /// Gets the value of an element as an <see cref="GameEngine.Authentication.ArcGISOAuthUserCredential">ArcGISOAuthUserCredential</see>.
        /// </summary>
        /// <returns>
        /// An <see cref="GameEngine.Authentication.ArcGISOAuthUserCredential">ArcGISOAuthUserCredential</see>.
        /// </returns>
        internal GameEngine.Authentication.ArcGISOAuthUserCredential GetValueAsOAuthUserCredential()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

            var localResult = PInvoke.RT_Element_getValueAsOAuthUserCredential(Handle, errorHandler);

            Unity.ArcGISErrorManager.CheckError(errorHandler);

            GameEngine.Authentication.ArcGISOAuthUserCredential localLocalResult = null;

            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new GameEngine.Authentication.ArcGISOAuthUserCredential(localResult);
            }

            return localLocalResult;
        }

        /// <summary>
        /// Gets the value of an element as an <see cref="GameEngine.Authentication.ArcGISOAuthUserTokenInfo">ArcGISOAuthUserTokenInfo</see>.
        /// </summary>
        /// <returns>
        /// An <see cref="GameEngine.Authentication.ArcGISOAuthUserTokenInfo">ArcGISOAuthUserTokenInfo</see>.
        /// </returns>
        internal GameEngine.Authentication.ArcGISOAuthUserTokenInfo GetValueAsOAuthUserTokenInfo()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();

            var localResult = PInvoke.RT_Element_getValueAsOAuthUserTokenInfo(Handle, errorHandler);

            Unity.ArcGISErrorManager.CheckError(errorHandler);

            GameEngine.Authentication.ArcGISOAuthUserTokenInfo localLocalResult = null;

            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new GameEngine.Authentication.ArcGISOAuthUserTokenInfo(localResult);
            }

            return localLocalResult;
        }

        /// <summary>
        /// Gets the value of the element.
        /// </summary>
        /// <returns>
        /// An <see cref="">string</see>.
        /// </returns>
        internal string GetValueAsString()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsString(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            return Unity.Convert.FromArcGISString(localResult);
        }
        
        /// <summary>
        /// Gets the value of the element.
        /// </summary>
        /// <returns>
        /// An uint32_t.
        /// </returns>
        internal uint GetValueAsUInt32()
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localResult = PInvoke.RT_Element_getValueAsUInt32(Handle, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            return localResult;
        }
        
        /// <summary>
        /// Sets the value of the element to an <see cref="GameEngine.Geometry.ArcGISDatumTransformation">ArcGISDatumTransformation</see> instance.
        /// </summary>
        /// <param name="value">The instance.</param>
        internal void SetValueFromDatumTransformation(GameEngine.Geometry.ArcGISDatumTransformation value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            PInvoke.RT_Element_setValueFromDatumTransformation(Handle, localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Sets the value of this element to be a dictionary.
        /// </summary>
        /// <param name="value">The value.</param>
        internal void SetValueFromDictionary(Unity.ArcGISDictionaryBase value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            PInvoke.RT_Element_setValueFromDictionary(Handle, localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Sets the value of the element.
        /// </summary>
        /// <param name="value">The value.</param>
        internal void SetValueFromFloat64(double value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            PInvoke.RT_Element_setValueFromFloat64(Handle, value, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Sets the value of the element from an attribute.
        /// </summary>
        /// <param name="value">The value.</param>
        internal void SetValueFromGEAttribute(GameEngine.Attributes.ArcGISAttribute value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            PInvoke.RT_Element_setValueFromGEAttribute(Handle, localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Sets the value of the element to an <see cref="GameEngine.Geometry.ArcGISGeographicTransformationStep">ArcGISGeographicTransformationStep</see> instance.
        /// </summary>
        /// <param name="value">The instance.</param>
        internal void SetValueFromGeographicTransformationStep(GameEngine.Geometry.ArcGISGeographicTransformationStep value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            PInvoke.RT_Element_setValueFromGeographicTransformationStep(Handle, localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Sets the value of the element.
        /// </summary>
        /// <param name="value">The value.</param>
        internal void SetValueFromGeometry(GameEngine.Geometry.ArcGISGeometry value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            PInvoke.RT_Element_setValueFromGeometry(Handle, localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Sets the value of the element from a visualization attribute.
        /// </summary>
        /// <param name="value">The value.</param>
        internal void SetValueFromGEVisualizationAttribute(GameEngine.Attributes.ArcGISVisualizationAttribute value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            PInvoke.RT_Element_setValueFromGEVisualizationAttribute(Handle, localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Sets the value of the element from a visualization attribute description.
        /// </summary>
        /// <param name="value">The value.</param>
        internal void SetValueFromGEVisualizationAttributeDescription(GameEngine.Attributes.ArcGISVisualizationAttributeDescription value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            PInvoke.RT_Element_setValueFromGEVisualizationAttributeDescription(Handle, localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Sets the value of the element to an <see cref="GameEngine.Geometry.ArcGISHorizontalVerticalTransformationStep">ArcGISHorizontalVerticalTransformationStep</see> instance.
        /// </summary>
        /// <param name="value">The instance.</param>
        internal void SetValueFromHorizontalVerticalTransformationStep(GameEngine.Geometry.ArcGISHorizontalVerticalTransformationStep value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localValue = value.Handle;
            
            PInvoke.RT_Element_setValueFromHorizontalVerticalTransformationStep(Handle, localValue, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Sets the value of the element.
        /// </summary>
        /// <param name="value">The value.</param>
        internal void SetValueFromString(string value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            PInvoke.RT_Element_setValueFromString(Handle, value, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        
        /// <summary>
        /// Sets the value of the element.
        /// </summary>
        /// <param name="value">The value.</param>
        internal void SetValueFromUInt32(uint value)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            PInvoke.RT_Element_setValueFromUInt32(Handle, value, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
        }
        #endregion // Methods
        
        #region Internal Members
        internal ArcGISElement(IntPtr handle) => Handle = handle;
        
        ~ArcGISElement()
        {
            if (Handle != IntPtr.Zero)
            {
                var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
                
                PInvoke.RT_Element_destroy(Handle, errorHandler);
                
                Unity.ArcGISErrorManager.CheckError(errorHandler);
            }
        }
        
        internal IntPtr Handle { get; set; }
        #endregion // Internal Members
    }
    
    internal static partial class PInvoke
    {
        #region P-Invoke Declarations
        [DllImport(Unity.Interop.Dll)]
        internal static extern ArcGISElementType RT_Element_getObjectType(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromArcGISCredential(IntPtr value, IntPtr errorHandler);

        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromArcGISTokenInfo(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromDatumTransformation(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromDictionary(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromFloat64(double value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromGEAttribute(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromGEBuildingSceneLayerAttributeStatistics(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromGeographicTransformationStep(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromGeometry(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromGEVisualizationAttribute(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromGEVisualizationAttributeDescription(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromHorizontalVerticalTransformationStep(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromOAuthUserCredential(IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromOAuthUserTokenInfo(IntPtr value, IntPtr errorHandler);
            
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromString([MarshalAs(UnmanagedType.LPStr)]string value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_fromUInt32(uint value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsArcGISCredential(IntPtr handle, IntPtr errorHandler);

        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsArcGISTokenInfo(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsDatumTransformation(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsDictionary(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern double RT_Element_getValueAsFloat64(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsGEAttribute(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsGEBuildingSceneLayerAttributeStatistics(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsGeographicTransformationStep(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsGeometry(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsGEVisualizationAttribute(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsGEVisualizationAttributeDescription(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsHorizontalVerticalTransformationStep(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsOAuthUserCredential(IntPtr handle, IntPtr errorHandler);

        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsOAuthUserTokenInfo(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_Element_getValueAsString(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern uint RT_Element_getValueAsUInt32(IntPtr handle, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromDatumTransformation(IntPtr handle, IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromDictionary(IntPtr handle, IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromFloat64(IntPtr handle, double value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromGEAttribute(IntPtr handle, IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromGeographicTransformationStep(IntPtr handle, IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromGeometry(IntPtr handle, IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromGEVisualizationAttribute(IntPtr handle, IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromGEVisualizationAttributeDescription(IntPtr handle, IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromHorizontalVerticalTransformationStep(IntPtr handle, IntPtr value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromString(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)]string value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_setValueFromUInt32(IntPtr handle, uint value, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern void RT_Element_destroy(IntPtr handle, IntPtr errorHandle);
        #endregion // P-Invoke Declarations
    }
}