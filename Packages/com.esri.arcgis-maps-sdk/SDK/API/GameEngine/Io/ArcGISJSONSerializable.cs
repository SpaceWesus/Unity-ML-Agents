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
using System;

namespace Esri.GameEngine.Io
{
    /// <summary>
    /// An interface for reading and writing JSON.
    /// </summary>
    /// <since>1.0.0</since>
    public partial interface ArcGISJSONSerializable<T>
    {
        #region Methods
        /// <summary>
        /// Convert an object to JSON string.
        /// </summary>
        /// <returns>
        /// A <see cref="">string</see> which is the JSON string.
        /// </returns>
        /// <since>1.0.0</since>
        string ToJSON();
        #endregion // Methods
    }
}