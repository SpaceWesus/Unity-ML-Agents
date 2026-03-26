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

namespace Esri.GameEngine.Geometry
{
    /// <summary>
    /// Converts between Points and formatted coordinate notation
    /// strings such as decimal degrees; degrees, minutes, and seconds;
    /// U.S. National Grid (USNG); and Military Grid Reference System
    /// (MGRS).
    /// </summary>
    /// <since>1.0.0</since>
    [StructLayout(LayoutKind.Sequential)]
    public partial class ArcGISCoordinateFormatter
    {
        #region Methods
        /// <summary>
        /// Parses a coordinate in Global Area Reference System (GARS) notation, and
        /// returns a Point representing that location.
        /// </summary>
        /// <remarks>
        /// The spatial reference provided must have an ellipsoid and
        /// datum matching those used by the source of the GARS string.
        /// If no spatial reference is provided, it is assumed the GARS string
        /// is referenced to WGS 84.
        /// <table>
        /// <tr><th>GARS notation examples
        /// <tr><td>354ND
        /// <tr><td>354ND22
        /// </table>
        /// Returns null on error, including when the given string is not valid
        /// GARS notation.
        /// </remarks>
        /// <param name="coordinates">The GARS notation string for the coordinate.</param>
        /// <param name="spatialReference">A spatial reference that defines the datum and ellipsoid referenced by the GARS coordinate.</param>
        /// <param name="GARSConversionMode">Select whether the returned point's location represents the south-west corner of the GARS cell the coordinate lies within, or its center.</param>
        /// <returns>
        /// A point with the location from the GARS string in the
        /// spatial reference provided.
        /// </returns>
        /// <since>1.0.0</since>
        public static ArcGISPoint FromGARS(string coordinates, ArcGISSpatialReference spatialReference, ArcGISGARSConversionMode GARSConversionMode)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localSpatialReference = spatialReference == null ? System.IntPtr.Zero : spatialReference.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_fromGARS(coordinates, localSpatialReference, GARSConversionMode, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISPoint localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new ArcGISPoint(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Parses a coordinate in World Geographic Reference System (GEOREF) notation,
        /// and returns a Point representing that location.
        /// </summary>
        /// <remarks>
        /// The spatial reference provided must have an ellipsoid and
        /// datum matching those used by the source of the GEOREF string.
        /// If no spatial reference is provided, it is assumed the GEOREF string
        /// is referenced to WGS 84.
        /// The GEOREF string may contain leading and trailing whitespace.
        /// <table>
        /// <tr><th>GEOREF notation examples
        /// <tr><td>MKML5056
        /// <tr><td>MKML50285665
        /// </table>
        /// Returns null on error, including when the given string is not valid
        /// GEOREF notation.
        /// </remarks>
        /// <param name="coordinates">The GEOREF notation string for the coordinate.</param>
        /// <param name="spatialReference">A spatial reference that defines the datum and ellipsoid referenced by the GEOREF coordinate.</param>
        /// <returns>
        /// A point with the location from the GEOREF string in the
        /// spatial reference provided.
        /// </returns>
        /// <since>1.0.0</since>
        public static ArcGISPoint FromGeoRef(string coordinates, ArcGISSpatialReference spatialReference)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localSpatialReference = spatialReference == null ? System.IntPtr.Zero : spatialReference.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_fromGeoRef(coordinates, localSpatialReference, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISPoint localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new ArcGISPoint(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Parses a coordinate in latitude-longitude notation, and returns a Point
        /// representing that location. The coordinate may use decimal degrees, degrees
        /// and decimal minutes, or degrees, minutes, and seconds format.
        /// </summary>
        /// <remarks>
        /// The spatial reference provided must have an ellipsoid and datum
        /// matching those used by the source of the latitude-longitude
        /// string.
        /// If no spatial reference is provided, it is assumed the latitude-longitude
        /// coordinates are referenced to WGS 84.
        /// <table>
        /// <caption>Supported characters</caption>
        /// <tr><th>Symbol             <th>Character <th>Name                        <th>Unicode number <th>HTML code
        /// <tr><td rowspan="7">Degree <td>*         <td>Asterisk                    <td>U+002A         <td>&#42;
        /// <tr>                       <td>^         <td>Circumflex Accent           <td>U+005E         <td>&#94;
        /// <tr>                       <td>~         <td>Tilde                       <td>U+007E         <td>&#126;
        /// <tr>                       <td>°         <td>Degree Sign                 <td>U+00B0         <td>&#176;
        /// <tr>                       <td>º         <td>Masculine Ordinal Indicator <td>U+00BA         <td>&#186;
        /// <tr>                       <td>˚         <td>Ring Above                  <td>U+02DA         <td>&#730;
        /// <tr>                       <td>⁰         <td>Superscript Zero            <td>U+2070         <td>&#8304;
        /// <tr><td rowspan="3">Minute <td>'         <td>Apostrophe                  <td>U+0027         <td>&#39;
        /// <tr>                       <td>’         <td>Right Single Quotation Mark <td>U+2019         <td>&#8127;
        /// <tr>                       <td>′         <td>Prime                       <td>U+2032         <td>&#8242;
        /// <tr><td rowspan="4">Second <td>"         <td>Quotation Mark              <td>U+0022         <td>&#34;
        /// <tr>                       <td>˝         <td>Double Acute Accent         <td>U+02DD         <td>&#733;
        /// <tr>                       <td>”         <td>Right Double Quotation Mark <td>U+201D         <td>&#8221;
        /// <tr>                       <td>″         <td>Double Prime                <td>U+2033         <td>&#8243;
        /// </table>
        /// The latitude-longitude string may contain leading and trailing
        /// whitespace, and may also contain a space, comma, or vertical bar symbol to
        /// separate the latitude from the longitude. It may also contain the characters
        /// 'N', 'S', 'E' and 'W', or use a minus (-) symbol to indicate the hemisphere of
        /// each value.
        /// <table>
        /// <tr><th>Latitude-longitude notation examples
        /// <tr><td>55 56 39.123N 003 09 43.034W
        /// <tr><td>55°56′39″N 3°09′43″W
        /// <tr><td>55~56.65205', -003~09.71723'
        /// <tr><td>55.9442008* | -3.1619539*
        /// </table>
        /// Returns null on error, including when the given string cannot be
        /// interpreted.
        /// </remarks>
        /// <param name="coordinates">The latitude-longitude notation string for the coordinate.</param>
        /// <param name="spatialReference">A spatial reference that defines the datum and ellipsoid referenced by the latitude-longitude coordinate.</param>
        /// <returns>
        /// A point with the location from the coordinate string in the
        /// spatial reference provided.
        /// </returns>
        /// <since>1.0.0</since>
        public static ArcGISPoint FromLatitudeLongitude(string coordinates, ArcGISSpatialReference spatialReference)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localSpatialReference = spatialReference == null ? System.IntPtr.Zero : spatialReference.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_fromLatitudeLongitude(coordinates, localSpatialReference, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISPoint localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new ArcGISPoint(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Parses a coordinate in Military Grid Reference System (MGRS) notation, and
        /// returns a Point representing that location.
        /// </summary>
        /// <remarks>
        /// The spatial reference provided must have an ellipsoid and datum
        /// matching those used by the source of the MGRS string.
        /// If no spatial reference is provided, it is assumed the MGRS string
        /// is referenced to WGS 84.
        /// For an explanation of the different modes for interpreting an MGRS
        /// notation string, please see <see cref="GameEngine.Geometry.ArcGISMGRSConversionMode">ArcGISMGRSConversionMode</see>. Note that the
        /// choice between zone 01 and 60 has no impact when reading from an
        /// MGRS notation string.
        /// The MGRS string can contain leading and trailing whitespace and can
        /// have whitespace between the grid zone designator, the 100km square
        /// identifier, and the numerical eastings and northings.
        /// <table>
        /// <tr><th>MGRS notation examples
        /// <tr><td>30UVG898998
        /// <tr><td>30UVG 89885 99877
        /// </table>
        /// Returns null on error, including when the given string is not valid
        /// MGRS notation.
        /// </remarks>
        /// <param name="coordinates">The MGRS notation string for the coordinate.</param>
        /// <param name="spatialReference">A spatial reference that defines the datum and ellipsoid referenced by the MGRS coordinate.</param>
        /// <param name="MGRSConversionMode">The mode used by the given MGRS coordinates.</param>
        /// <returns>
        /// A point with the location from the MGRS string in the
        /// spatial reference provided.
        /// </returns>
        /// <seealso cref="GameEngine.Geometry.ArcGISMGRSConversionMode">ArcGISMGRSConversionMode</seealso>
        /// <since>1.0.0</since>
        public static ArcGISPoint FromMGRS(string coordinates, ArcGISSpatialReference spatialReference, ArcGISMGRSConversionMode MGRSConversionMode)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localSpatialReference = spatialReference == null ? System.IntPtr.Zero : spatialReference.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_fromMGRS(coordinates, localSpatialReference, MGRSConversionMode, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISPoint localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new ArcGISPoint(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Parses a coordinate in United States National Grid (USNG) notation, and
        /// returns a Point representing that location.
        /// </summary>
        /// <remarks>
        /// The spatial reference provided must have an ellipsoid and datum
        /// matching those used by the source of the USNG string.
        /// If no spatial reference is provided and the USNG string is suffixed
        /// with "(NAD 27)", it is assumed the USNG string is referenced to
        /// NAD 27.
        /// If no spatial reference is provided and no such suffix exists, it is
        /// assumed the USNG string is referenced to WGS 84.
        /// The USNG string can contain leading and trailing whitespace and can
        /// have whitespace between the grid zone designator, the 100km square
        /// identifier and the numerical eastings and northings.
        /// <table>
        /// <tr><th>USNG notation examples
        /// <tr><td>13TFJ 23 59
        /// <tr><td>13TFJ2374159574
        /// </table>
        /// Returns null on error, including when the given string is not valid
        /// USNG notation.
        /// </remarks>
        /// <param name="coordinates">The USNG notation string for the coordinate.</param>
        /// <param name="spatialReference">A spatial reference that defines the datum and ellipsoid referenced by the USNG coordinate.</param>
        /// <returns>
        /// A point with the location from the USNG string in the
        /// spatial reference provided.
        /// </returns>
        /// <seealso cref="GameEngine.Geometry.ArcGISMGRSConversionMode">ArcGISMGRSConversionMode</seealso>
        /// <since>1.0.0</since>
        public static ArcGISPoint FromUSNG(string coordinates, ArcGISSpatialReference spatialReference)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localSpatialReference = spatialReference == null ? System.IntPtr.Zero : spatialReference.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_fromUSNG(coordinates, localSpatialReference, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISPoint localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new ArcGISPoint(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Parses a coordinate in Universal Transverse Mercator (UTM) notation, and
        /// returns a Point representing that location.
        /// </summary>
        /// <remarks>
        /// The spatial reference provided must have an ellipsoid and datum
        /// matching those used by the source of the UTM string.
        /// If no spatial reference is provided, it is assumed the UTM string is
        /// referenced to WGS 84.
        /// The UTM string can contain leading and trailing whitespace and can
        /// have whitespace between the zone and latitude designator and the
        /// numerical eastings and northings.
        /// <table>
        /// <tr><th>UTM notation examples
        /// <tr><td>30U 489885 6199877
        /// <tr><td>30U4898856199877
        /// <tr><td>30N 489885 6199877 (using N/S indicator)
        /// <tr><td>489885.32,6199877.36,30U (this form supports sub-meter precision)
        /// </table>
        /// Returns null on error, including when the given string is not valid
        /// UTM notation.
        /// </remarks>
        /// <param name="coordinates">The UTM notation string for the coordinate.</param>
        /// <param name="spatialReference">A spatial reference that defines the datum and ellipsoid referenced by the UTM coordinate.</param>
        /// <param name="UTMConversionMode">The latitude notation scheme used by the given UTM coordinate, either a latitudinal band, or a hemisphere designator.</param>
        /// <returns>
        /// A point with the location from the UTM string in the
        /// spatial reference provided.
        /// </returns>
        /// <seealso cref="GameEngine.Geometry.ArcGISMGRSConversionMode">ArcGISMGRSConversionMode</seealso>
        /// <since>1.0.0</since>
        public static ArcGISPoint FromUTM(string coordinates, ArcGISSpatialReference spatialReference, ArcGISUTMConversionMode UTMConversionMode)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localSpatialReference = spatialReference == null ? System.IntPtr.Zero : spatialReference.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_fromUTM(coordinates, localSpatialReference, UTMConversionMode, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            ArcGISPoint localLocalResult = null;
            
            if (localResult != IntPtr.Zero)
            {
                localLocalResult = new ArcGISPoint(localResult);
            }
            
            return localLocalResult;
        }
        
        /// <summary>
        /// Returns a formatted coordinate in Global Area Reference System (GARS) notation
        /// representing the given point's location.
        /// </summary>
        /// <remarks>
        /// The point must have a spatial reference.
        /// Returns null on error.
        /// </remarks>
        /// <param name="point">The location to be represented in GARS notation.</param>
        /// <returns>
        /// A GARS notation string representing the GARS cell containing the
        /// given point.
        /// </returns>
        /// <since>1.0.0</since>
        public static string ToGARS(ArcGISPoint point)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localPoint = point.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_toGARS(localPoint, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            return Unity.Convert.FromArcGISString(localResult);
        }
        
        /// <summary>
        /// Returns a formatted coordinate in World Geographic Reference System (GEOREF) notation
        /// representing the given point's location.
        /// </summary>
        /// <remarks>
        /// The precision value controls the number of digits used to represent
        /// the fractional part of coordinate's latitude and longitude,
        /// expressed in minutes. For example:
        /// <table>
        /// <tr><th>precision <th>Example output <th>Angular precision <th>Approx precision
        /// <tr><td>0         <td>MKML5056       <td>1min              <td>2km
        /// <tr><td>1         <td>MKML5056       <td>1min              <td>2km
        /// <tr><td>2         <td>MKML5056       <td>1min              <td>2km
        /// <tr><td>3         <td>MKML502566     <td>0.1min            <td>200m
        /// <tr><td>4         <td>MKML50285665   <td>0.01min           <td>20m
        /// <tr><td>5         <td>MKML5028256652 <td>0.001min          <td>2m
        /// </table>
        /// 
        /// At the equator to 1 significant figure.
        /// The 'precision' should be in the interval [0, 9].
        /// The point must have a spatial reference.
        /// Returns null on error.
        /// </remarks>
        /// <param name="point">The location to be represented in GEOREF notation.</param>
        /// <param name="precision">The precision with which to represent the coordinate.</param>
        /// <returns>
        /// A GEOREF notation string representing the position of the given point.
        /// </returns>
        /// <since>1.0.0</since>
        public static string ToGeoRef(ArcGISPoint point, int precision)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localPoint = point.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_toGeoRef(localPoint, precision, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            return Unity.Convert.FromArcGISString(localResult);
        }
        
        /// <summary>
        /// Returns a formatted coordinate in latitude-longitude notation representing
        /// the given point's location.
        /// </summary>
        /// <remarks>
        /// The latitude-longitude string contains a space separating the latitude
        /// from the longitude value, and the characters 'N' or 'S', and 'E' and 'W',
        /// to indicate the hemisphere of each value. The string also contains spaces
        /// separating the components (degrees, minutes, seconds) of each value.
        /// The precision of the output is controlled by both the 'format' and
        /// 'decimal_places' parameters. For example:
        /// <table>
        /// <tr><th>decimal_places     <th>format                                            <th>Example output               <th>Angular precision <th>Approx precision
        /// <tr><td>0                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DecimalDegrees">ArcGISLatitudeLongitudeFormat.DecimalDegrees</see>        <td>056N 0003W                   <td>1°                <td>100km
        /// <tr><td>1                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DecimalDegrees">ArcGISLatitudeLongitudeFormat.DecimalDegrees</see>        <td>55.9N 003.2W                 <td>0.1°              <td>10km
        /// <tr><td>2                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DecimalDegrees">ArcGISLatitudeLongitudeFormat.DecimalDegrees</see>        <td>55.94N 003.16W               <td>0.01°             <td>1km
        /// <tr><td>3                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DecimalDegrees">ArcGISLatitudeLongitudeFormat.DecimalDegrees</see>        <td>55.944N 003.162W             <td>0.001°            <td>100m
        /// <tr><td>0                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DegreesDecimalMinutes">ArcGISLatitudeLongitudeFormat.DegreesDecimalMinutes</see> <td>55 057N 003 010W             <td>1min              <td>2km
        /// <tr><td>1                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DegreesDecimalMinutes">ArcGISLatitudeLongitudeFormat.DegreesDecimalMinutes</see> <td>55 56.7N 003 09.7W           <td>0.1min            <td>200m
        /// <tr><td>2                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DegreesDecimalMinutes">ArcGISLatitudeLongitudeFormat.DegreesDecimalMinutes</see> <td>55 56.65N 003 09.72W         <td>0.01min           <td>20m
        /// <tr><td>3                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DegreesDecimalMinutes">ArcGISLatitudeLongitudeFormat.DegreesDecimalMinutes</see> <td>55 56.650N 003 09.717W       <td>0.001min          <td>2m
        /// <tr><td>0                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DegreesMinutesSeconds">ArcGISLatitudeLongitudeFormat.DegreesMinutesSeconds</see> <td>55 56 039N 003 09 043W       <td>1sec              <td>30m
        /// <tr><td>1                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DegreesMinutesSeconds">ArcGISLatitudeLongitudeFormat.DegreesMinutesSeconds</see> <td>55 56 39.1N 003 09 43.0W     <td>0.1sec            <td>3m
        /// <tr><td>2                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DegreesMinutesSeconds">ArcGISLatitudeLongitudeFormat.DegreesMinutesSeconds</see> <td>55 56 39.12N 003 09 43.03W   <td>0.01sec           <td>300mm
        /// <tr><td>3                  <td><see cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat.DegreesMinutesSeconds">ArcGISLatitudeLongitudeFormat.DegreesMinutesSeconds</see> <td>55 56 39.123N 003 09 43.034W <td>0.001sec          <td>30mm
        /// </table>
        /// 
        /// At the equator to 1 significant figure.
        /// 'decimal_places' should be in the interval [0, 16].
        /// The point must have a spatial reference.
        /// Returns null on error.
        /// </remarks>
        /// <param name="point">The location to be represented as a formatted latitude-longitude string.</param>
        /// <param name="format">The mode to use when formatting the latitude-longitude string.</param>
        /// <param name="decimalPlaces">The number of decimal places to use.</param>
        /// <returns>
        /// A string representing the latitude-longitude of the
        /// given point.
        /// </returns>
        /// <seealso cref="GameEngine.Geometry.ArcGISLatitudeLongitudeFormat">ArcGISLatitudeLongitudeFormat</seealso>
        /// <since>1.0.0</since>
        public static string ToLatitudeLongitude(ArcGISPoint point, ArcGISLatitudeLongitudeFormat format, int decimalPlaces)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localPoint = point.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_toLatitudeLongitude(localPoint, format, decimalPlaces, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            return Unity.Convert.FromArcGISString(localResult);
        }
        
        /// <summary>
        /// Returns a formatted coordinate in Military Grid Reference System (MGRS)
        /// notation representing the given point's location.
        /// </summary>
        /// <remarks>
        /// For an explanation of the different modes for interpreting an MGRS
        /// notation string, please see <see cref="GameEngine.Geometry.ArcGISMGRSConversionMode">ArcGISMGRSConversionMode</see>. Note that the
        /// choice between zone 01 and 60 has an impact only when generating the
        /// MGRS notation string for a point with longitude of exactly 180deg.
        /// The precision value controls the number of digits used to represent each
        /// numerical easting and northing value within the MGRS string. For example:
        /// <table>
        /// <tr><th>precision <th>add_spaces <th>Example output     <th>Approx precision
        /// <tr><td>0         <td>false      <td>30UVG              <td>100km
        /// <tr><td>1         <td>false      <td>30UVG89            <td>10km
        /// <tr><td>2         <td>false      <td>30UVG8999          <td>1km
        /// <tr><td>3         <td>false      <td>30UVG898998        <td>100m
        /// <tr><td>4         <td>false      <td>30UVG89889988      <td>10m
        /// <tr><td>5         <td>false      <td>30UVG8988499881    <td>1m
        /// <tr><td>0         <td>true       <td>30U VG             <td>100km
        /// <tr><td>1         <td>true       <td>30U VG 8 9         <td>10km
        /// <tr><td>2         <td>true       <td>30U VG 89 99       <td>1km
        /// <tr><td>3         <td>true       <td>30U VG 898 998     <td>100m
        /// <tr><td>4         <td>true       <td>30U VG 8988 9988   <td>10m
        /// <tr><td>5         <td>true       <td>30U VG 89884 99881 <td>1m
        /// </table>
        /// 
        /// The 'precision' should be in the interval [0, 8].
        /// The point must have a spatial reference.
        /// Returns null on error.
        /// </remarks>
        /// <param name="point">The location to be represented in MGRS notation.</param>
        /// <param name="MGRSConversionMode">The mode to use for the returned MGRS notation string.</param>
        /// <param name="precision">The precision with which to represent the coordinate.</param>
        /// <param name="addSpaces">If false, the generated string contains no spaces. If true, a space separates the grid zone designator, the 100km square identifier, and the numerical easting and northing values.</param>
        /// <returns>
        /// An MGRS notation string representing the position of the given point.
        /// </returns>
        /// <since>1.0.0</since>
        public static string ToMGRS(ArcGISPoint point, ArcGISMGRSConversionMode MGRSConversionMode, int precision, bool addSpaces)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localPoint = point.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_toMGRS(localPoint, MGRSConversionMode, precision, addSpaces, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            return Unity.Convert.FromArcGISString(localResult);
        }
        
        /// <summary>
        /// Returns a formatted coordinate in United States National Grid (USNG)
        /// notation representing the given point's location.
        /// </summary>
        /// <remarks>
        /// The precision value controls the number of digits used to represent each
        /// numerical easting and northing value within the USNG string. For example:
        /// 
        /// <table>
        /// <tr><th>precision <th>add_spaces <th>Example output     <th>Approx precision
        /// <tr><td>0         <td>false      <td>13TFJ              <td>100km
        /// <tr><td>1         <td>false      <td>13TFJ25            <td>10km
        /// <tr><td>2         <td>false      <td>13TFJ2359          <td>1km
        /// <tr><td>3         <td>false      <td>13TFJ237595        <td>100m
        /// <tr><td>4         <td>false      <td>13TFJ23745951      <td>10m
        /// <tr><td>5         <td>false      <td>13TFJ2374359512    <td>1m
        /// <tr><td>0         <td>true       <td>13T FJ             <td>100km
        /// <tr><td>1         <td>true       <td>13T FJ 2 5         <td>10km
        /// <tr><td>2         <td>true       <td>13T FJ 23 59       <td>1km
        /// <tr><td>3         <td>true       <td>13T FJ 237 595     <td>100m
        /// <tr><td>4         <td>true       <td>13T FJ 2374 5951   <td>10m
        /// <tr><td>5         <td>true       <td>13T FJ 23743 59512 <td>1m
        /// </table>
        /// 
        /// If the point's <see cref="GameEngine.Geometry.ArcGISSpatialReference">ArcGISSpatialReference</see> is NAD 27, then a suffix is added to the end of the formatted string.
        /// For example:
        /// 
        /// <table>
        /// <tr><th>precision <th>add_spaces <th>Example output         <th>Approx precision
        /// <tr><td>4         <td>false      <td>13TFJ23795929 (NAD 27) <td>10km
        /// <tr><td>2         <td>true       <td>13T FJ 23 59 (NAD 27)  <td>1km
        /// </table>
        /// 
        /// When the point's spatial reference is based on NAD 27.
        /// The 'precision' should be in the interval [0, 8].
        /// The point must have a spatial reference.
        /// Returns null on error.
        /// </remarks>
        /// <param name="point">The coordinate to be represented in MGRS notation.</param>
        /// <param name="precision">The precision with which to represent the coordinate.</param>
        /// <param name="addSpaces">If false, the generated string contains no spaces. If true, a space separates the grid zone designator, the 100km square identifier, and the numerical easting and northing values.</param>
        /// <returns>
        /// A USNG notation string representing the position of the given point.
        /// </returns>
        /// <since>1.0.0</since>
        public static string ToUSNG(ArcGISPoint point, int precision, bool addSpaces)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localPoint = point.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_toUSNG(localPoint, precision, addSpaces, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            return Unity.Convert.FromArcGISString(localResult);
        }
        
        /// <summary>
        /// Returns a formatted coordinate in Universal Transverse Mercator (UTM)
        /// notation representing the given point's location.
        /// </summary>
        /// <remarks>
        /// Example output for a point in the southern hemisphere:
        /// <table>
        /// <tr><th>utm_conversion_mode                           <th>add_spaces <th>Example output
        /// <tr><td> <see cref="GameEngine.Geometry.ArcGISUTMConversionMode.LatitudeBandIndicators">ArcGISUTMConversionMode.LatitudeBandIndicators</see> <td>false      <td>30U4898846199881
        /// <tr><td> <see cref="GameEngine.Geometry.ArcGISUTMConversionMode.LatitudeBandIndicators">ArcGISUTMConversionMode.LatitudeBandIndicators</see> <td>true       <td>30U 489884 6199881
        /// <tr><td> <see cref="GameEngine.Geometry.ArcGISUTMConversionMode.NorthSouthIndicators">ArcGISUTMConversionMode.NorthSouthIndicators</see>   <td>false      <td>30N4898846199881
        /// <tr><td> <see cref="GameEngine.Geometry.ArcGISUTMConversionMode.NorthSouthIndicators">ArcGISUTMConversionMode.NorthSouthIndicators</see>   <td>true       <td>30N 489884 6199881
        /// </table>
        /// 
        /// The point must have a spatial reference.
        /// Returns null on error.
        /// </remarks>
        /// <param name="point">The coordinate to be represented in UTM notation.</param>
        /// <param name="UTMConversionMode">The latitude notation scheme to use in the returned UTM notation string, either a latitudinal band, or a hemisphere designator.</param>
        /// <param name="addSpaces">If false, the generated string contains no spaces. If true, a space separates the UTM zone and latitude designator, and each numerical easting and northing value.</param>
        /// <returns>
        /// A UTM notation string representing the position of the given point.
        /// </returns>
        /// <since>1.0.0</since>
        public static string ToUTM(ArcGISPoint point, ArcGISUTMConversionMode UTMConversionMode, bool addSpaces)
        {
            var errorHandler = Unity.ArcGISErrorManager.CreateHandler();
            
            var localPoint = point.Handle;
            
            var localResult = PInvoke.RT_CoordinateFormatter_toUTM(localPoint, UTMConversionMode, addSpaces, errorHandler);
            
            Unity.ArcGISErrorManager.CheckError(errorHandler);
            
            return Unity.Convert.FromArcGISString(localResult);
        }
        #endregion // Methods
        
        #region Internal Members
        #endregion // Internal Members
    }
    
    internal static partial class PInvoke
    {
        #region P-Invoke Declarations
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_fromGARS([MarshalAs(UnmanagedType.LPStr)]string coordinates, IntPtr spatialReference, ArcGISGARSConversionMode GARSConversionMode, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_fromGeoRef([MarshalAs(UnmanagedType.LPStr)]string coordinates, IntPtr spatialReference, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_fromLatitudeLongitude([MarshalAs(UnmanagedType.LPStr)]string coordinates, IntPtr spatialReference, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_fromMGRS([MarshalAs(UnmanagedType.LPStr)]string coordinates, IntPtr spatialReference, ArcGISMGRSConversionMode MGRSConversionMode, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_fromUSNG([MarshalAs(UnmanagedType.LPStr)]string coordinates, IntPtr spatialReference, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_fromUTM([MarshalAs(UnmanagedType.LPStr)]string coordinates, IntPtr spatialReference, ArcGISUTMConversionMode UTMConversionMode, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_toGARS(IntPtr point, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_toGeoRef(IntPtr point, int precision, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_toLatitudeLongitude(IntPtr point, ArcGISLatitudeLongitudeFormat format, int decimalPlaces, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_toMGRS(IntPtr point, ArcGISMGRSConversionMode MGRSConversionMode, int precision, [MarshalAs(UnmanagedType.I1)]bool addSpaces, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_toUSNG(IntPtr point, int precision, [MarshalAs(UnmanagedType.I1)]bool addSpaces, IntPtr errorHandler);
        
        [DllImport(Unity.Interop.Dll)]
        internal static extern IntPtr RT_CoordinateFormatter_toUTM(IntPtr point, ArcGISUTMConversionMode UTMConversionMode, [MarshalAs(UnmanagedType.I1)]bool addSpaces, IntPtr errorHandler);
        #endregion // P-Invoke Declarations
    }
}