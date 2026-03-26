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
namespace Esri.Standard
{
    /// <summary>
    /// The list of possible generic errors.
    /// </summary>
    /// <remarks>
    /// This is used in the <see cref="GameEngine.ArcGISRuntimeEnvironmentErrorEvent">ArcGISRuntimeEnvironmentErrorEvent</see> error handler function.
    /// </remarks>
    /// <seealso cref="GameEngine.ArcGISRuntimeEnvironmentErrorEvent">ArcGISRuntimeEnvironmentErrorEvent</seealso>
    /// <since>1.0.0</since>
    public enum ArcGISErrorType
    {
        /// <summary>
        /// Unknown error.
        /// </summary>
        /// <remarks>
        /// The catch-all for unknown errors.
        /// </remarks>
        /// <since>1.0.0</since>
        Unknown = -1,
        
        /// <summary>
        /// Success.
        /// </summary>
        /// <remarks>
        /// Indicates success, not an error.
        /// </remarks>
        /// <since>1.0.0</since>
        Success = 0,
        
        /// <summary>
        /// A null pointer.
        /// </summary>
        /// <since>1.0.0</since>
        CommonNullPtr = 1,
        
        /// <summary>
        /// Invalid argument.
        /// </summary>
        /// <since>1.0.0</since>
        CommonInvalidArgument = 2,
        
        /// <summary>
        /// Not implemented.
        /// </summary>
        /// <since>1.0.0</since>
        CommonNotImplemented = 3,
        
        /// <summary>
        /// Out of range.
        /// </summary>
        /// <since>1.0.0</since>
        CommonOutOfRange = 4,
        
        /// <summary>
        /// Invalid access.
        /// </summary>
        /// <since>1.0.0</since>
        CommonInvalidAccess = 5,
        
        /// <summary>
        /// Illegal state.
        /// </summary>
        /// <since>1.0.0</since>
        CommonIllegalState = 6,
        
        /// <summary>
        /// Not found.
        /// </summary>
        /// <since>1.0.0</since>
        CommonNotFound = 7,
        
        /// <summary>
        /// Entity exists.
        /// </summary>
        /// <since>1.0.0</since>
        CommonExists = 8,
        
        /// <summary>
        /// Timeout.
        /// </summary>
        /// <since>1.0.0</since>
        CommonTimeout = 9,
        
        /// <summary>
        /// Regular expression error.
        /// </summary>
        /// <since>1.0.0</since>
        CommonRegularExpression = 10,
        
        /// <summary>
        /// Property not supported.
        /// </summary>
        /// <since>1.0.0</since>
        CommonPropertyNotSupported = 11,
        
        /// <summary>
        /// No permission.
        /// </summary>
        /// <since>1.0.0</since>
        CommonNoPermission = 12,
        
        /// <summary>
        /// File error.
        /// </summary>
        /// <since>1.0.0</since>
        CommonFile = 13,
        
        /// <summary>
        /// File not found.
        /// </summary>
        /// <since>1.0.0</since>
        CommonFileNotFound = 14,
        
        /// <summary>
        /// Invalid call.
        /// </summary>
        /// <since>1.0.0</since>
        CommonInvalidCall = 15,
        
        /// <summary>
        /// IO error.
        /// </summary>
        /// <since>1.0.0</since>
        CommonIO = 16,
        
        /// <summary>
        /// User canceled.
        /// </summary>
        /// <since>1.0.0</since>
        CommonUserCanceled = 17,
        
        /// <summary>
        /// Internal error.
        /// </summary>
        /// <since>1.0.0</since>
        CommonInternalError = 18,
        
        /// <summary>
        /// Conversion failed.
        /// </summary>
        /// <since>1.0.0</since>
        CommonConversionFailed = 19,
        
        /// <summary>
        /// No data.
        /// </summary>
        /// <since>1.0.0</since>
        CommonNoData = 20,
        
        /// <summary>
        /// Invalid JSON.
        /// </summary>
        /// <since>1.0.0</since>
        CommonInvalidJSON = 21,
        
        /// <summary>
        /// Propagated error.
        /// </summary>
        /// <since>1.0.0</since>
        CommonUserDefinedFailure = 22,
        
        /// <summary>
        /// Invalid XML.
        /// </summary>
        /// <since>1.0.0</since>
        CommonBadXML = 23,
        
        /// <summary>
        /// Object is already owned.
        /// </summary>
        /// <since>1.0.0</since>
        CommonObjectAlreadyOwned = 24,
        
        /// <summary>
        /// The resource is past its expiry date.
        /// </summary>
        /// <since>1.0.0</since>
        CommonExpired = 26,
        
        /// <summary>
        /// Nullability violation.
        /// </summary>
        /// <remarks>
        /// A null was returned from a property or method which is
        /// expected to be non-nullable.
        /// </remarks>
        /// <since>1.0.0</since>
        CommonNullabilityViolation = 27,
        
        /// <summary>
        /// Invalid property.
        /// </summary>
        /// <remarks>
        /// The value of a property is invalid.
        /// </remarks>
        /// <since>1.0.0</since>
        CommonInvalidProperty = 28,
        
        /// <summary>
        /// SQLite error.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteError = 1001,
        
        /// <summary>
        /// SQLite internal error.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteInternal = 1002,
        
        /// <summary>
        /// SQLite permission.
        /// </summary>
        /// <since>1.0.0</since>
        SQLitePerm = 1003,
        
        /// <summary>
        /// SQLite operation aborted.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteAbort = 1004,
        
        /// <summary>
        /// SQLite database busy.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteBusy = 1005,
        
        /// <summary>
        /// SQLite database locked.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteLocked = 1006,
        
        /// <summary>
        /// SQLite out of memory.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteNoMem = 1007,
        
        /// <summary>
        /// SQLite read only.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteReadonly = 1008,
        
        /// <summary>
        /// SQLite operation interrupted.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteInterrupt = 1009,
        
        /// <summary>
        /// SQLite IO error.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteIOErr = 1010,
        
        /// <summary>
        /// SQLite corrupt database.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteCorrupt = 1011,
        
        /// <summary>
        /// SQLite not found.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteNotFound = 1012,
        
        /// <summary>
        /// SQLite disk full.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteFull = 1013,
        
        /// <summary>
        /// SQLite cannot open.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteCantOpen = 1014,
        
        /// <summary>
        /// SQLite file locking protocol.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteProtocol = 1015,
        
        /// <summary>
        /// SQLite empty error.
        /// </summary>
        /// <remarks>
        /// This code is not currently used.
        /// </remarks>
        /// <since>1.0.0</since>
        SQLiteEmpty = 1016,
        
        /// <summary>
        /// SQLite schema changed.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteSchema = 1017,
        
        /// <summary>
        /// SQLite string or data blob too large.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteTooBig = 1018,
        
        /// <summary>
        /// SQLite constraint violation.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteConstraint = 1019,
        
        /// <summary>
        /// SQLite data type mismatch.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteMismatch = 1020,
        
        /// <summary>
        /// SQLite interface misuse.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteMisuse = 1021,
        
        /// <summary>
        /// SQLite no large file support.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteNolfs = 1022,
        
        /// <summary>
        /// SQLite statement not authorized.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteAuth = 1023,
        
        /// <summary>
        /// SQLite format error.
        /// </summary>
        /// <remarks>
        /// This code is not currently used.
        /// </remarks>
        /// <since>1.0.0</since>
        SQLiteFormat = 1024,
        
        /// <summary>
        /// SQLite out of range.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteRange = 1025,
        
        /// <summary>
        /// Not an SQLite database.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteNotADatabase = 1026,
        
        /// <summary>
        /// SQLite unusual operation notice.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteNotice = 1027,
        
        /// <summary>
        /// SQLite unusual operation warning.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteWarning = 1028,
        
        /// <summary>
        /// SQLite row is available.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteRow = 1029,
        
        /// <summary>
        /// SQLite operation is complete.
        /// </summary>
        /// <since>1.0.0</since>
        SQLiteDone = 1030,
        
        /// <summary>
        /// Unknown geometry error.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryUnknownError = 2000,
        
        /// <summary>
        /// Corrupt geometry.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryCorruptedGeometry = 2001,
        
        /// <summary>
        /// Empty geometry.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryEmptyGeometry = 2002,
        
        /// <summary>
        /// Math singularity.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryMathSingularity = 2003,
        
        /// <summary>
        /// Geometry buffer too small.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryBufferIsTooSmall = 2004,
        
        /// <summary>
        /// Geometry invalid shape type.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryInvalidShapeType = 2005,
        
        /// <summary>
        /// Geometry projection out of supported range.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryProjectionOutOfSupportedRange = 2006,
        
        /// <summary>
        /// Non simple geometry.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryNonSimpleGeometry = 2007,
        
        /// <summary>
        /// Cannot calculate geodesic.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryCannotCalculateGeodesic = 2008,
        
        /// <summary>
        /// Geometry notation conversion.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryNotationConversion = 2009,
        
        /// <summary>
        /// Missing grid file.
        /// </summary>
        /// <since>1.0.0</since>
        GeometryMissingGridFile = 2010,
        
        /// <summary>
        /// Geodatabase value out of range.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseValueOutOfRange = 3001,
        
        /// <summary>
        /// Geodatabase data type mismatch.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseDataTypeMismatch = 3002,
        
        /// <summary>
        /// Geodatabase invalid XML.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseBadXML = 3003,
        
        /// <summary>
        /// Database already exists.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseDatabaseAlreadyExists = 3004,
        
        /// <summary>
        /// Database does not exist.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseDatabaseDoesNotExist = 3005,
        
        /// <summary>
        /// Geodatabase name longer than 128 characters.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseNameLongerThan128Characters = 3006,
        
        /// <summary>
        /// Geodatabase invalid shape type.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseInvalidShapeType = 3007,
        
        /// <summary>
        /// Geodatabase raster not supported.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseRasterNotSupported = 3008,
        
        /// <summary>
        /// Geodatabase relationship class one to one.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseRelationshipClassOneToOne = 3009,
        
        /// <summary>
        /// Geodatabase item not found.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseItemNotFound = 3010,
        
        /// <summary>
        /// Geodatabase duplicate code.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseDuplicateCode = 3011,
        
        /// <summary>
        /// Geodatabase missing code.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseMissingCode = 3012,
        
        /// <summary>
        /// Geodatabase wrong item type.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseWrongItemType = 3013,
        
        /// <summary>
        /// Geodatabase Id field not nullable.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseIdFieldNotNullable = 3014,
        
        /// <summary>
        /// Geodatabase default value not supported.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseDefaultValueNotSupported = 3015,
        
        /// <summary>
        /// Geodatabase table not editable.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseTableNotEditable = 3016,
        
        /// <summary>
        /// Geodatabase field not found.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseFieldNotFound = 3017,
        
        /// <summary>
        /// Geodatabase field exists.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseFieldExists = 3018,
        
        /// <summary>
        /// Geodatabase cannot alter field type.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotAlterFieldType = 3019,
        
        /// <summary>
        /// Geodatabase cannot alter field width.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotAlterFieldWidth = 3020,
        
        /// <summary>
        /// Geodatabase cannot alter field to nullable.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotAlterFieldToNullable = 3021,
        
        /// <summary>
        /// Geodatabase cannot alter field to editable.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotAlterFieldToEditable = 3022,
        
        /// <summary>
        /// Geodatabase cannot alter field to deletable.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotAlterFieldToDeletable = 3023,
        
        /// <summary>
        /// Geodatabase cannot alter geometry properties.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotAlterGeometryProperties = 3024,
        
        /// <summary>
        /// Geodatabase unnamed table.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseUnnamedTable = 3025,
        
        /// <summary>
        /// Geodatabase invalid type for domain.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseInvalidTypeForDomain = 3026,
        
        /// <summary>
        /// Geodatabase min max reversed.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseMinMaxReversed = 3027,
        
        /// <summary>
        /// Geodatabase field not supported on relationship class.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseFieldNotSupportedOnRelationshipClass = 3028,
        
        /// <summary>
        /// Geodatabase relationship class key.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseRelationshipClassKey = 3029,
        
        /// <summary>
        /// Geodatabase value is null.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseValueIsNull = 3030,
        
        /// <summary>
        /// Geodatabase multiple SQL statements.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseMultipleSQLStatements = 3031,
        
        /// <summary>
        /// Geodatabase no SQL statements.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseNoSQLStatements = 3032,
        
        /// <summary>
        /// Geodatabase geometry field missing.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseGeometryFieldMissing = 3033,
        
        /// <summary>
        /// Geodatabase transaction started.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseTransactionStarted = 3034,
        
        /// <summary>
        /// Geodatabase transaction not started.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseTransactionNotStarted = 3035,
        
        /// <summary>
        /// Geodatabase shape requires z.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseShapeRequiresZ = 3036,
        
        /// <summary>
        /// Geodatabase shape requires m.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseShapeRequiresM = 3037,
        
        /// <summary>
        /// Geodatabase shape no z.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseShapeNoZ = 3038,
        
        /// <summary>
        /// Geodatabase shape no m.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseShapeNoM = 3039,
        
        /// <summary>
        /// Geodatabase shape wrong type.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseShapeWrongType = 3040,
        
        /// <summary>
        /// Geodatabase cannot update field type.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotUpdateFieldType = 3041,
        
        /// <summary>
        /// Geodatabase no rows affected.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseNoRowsAffected = 3042,
        
        /// <summary>
        /// Geodatabase subtype invalid.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseSubtypeInvalid = 3043,
        
        /// <summary>
        /// Geodatabase subtype must be integer.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseSubtypeMustBeInteger = 3044,
        
        /// <summary>
        /// Geodatabase subtypes not enabled.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseSubtypesNotEnabled = 3045,
        
        /// <summary>
        /// Geodatabase subtype exists.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseSubtypeExists = 3046,
        
        /// <summary>
        /// Geodatabase duplicate field not allowed.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseDuplicateFieldNotAllowed = 3047,
        
        /// <summary>
        /// Geodatabase cannot delete field.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotDeleteField = 3048,
        
        /// <summary>
        /// Geodatabase index exists.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseIndexExists = 3049,
        
        /// <summary>
        /// Geodatabase index not found.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseIndexNotFound = 3050,
        
        /// <summary>
        /// Geodatabase cursor not on row.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCursorNotOnRow = 3051,
        
        /// <summary>
        /// Geodatabase internal error.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseInternalError = 3052,
        
        /// <summary>
        /// Geodatabase cannot write geodatabase managed fields.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotWriteGeodatabaseManagedFields = 3053,
        
        /// <summary>
        /// Geodatabase item already exists.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseItemAlreadyExists = 3054,
        
        /// <summary>
        /// Geodatabase invalid spatial index name.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseInvalidSpatialIndexName = 3055,
        
        /// <summary>
        /// Geodatabase requires spatial index.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseRequiresSpatialIndex = 3056,
        
        /// <summary>
        /// Geodatabase reserved name.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseReservedName = 3057,
        
        /// <summary>
        /// Geodatabase cannot update schema if change tracking.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotUpdateSchemaIfChangeTracking = 3058,
        
        /// <summary>
        /// Geodatabase invalid date.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseInvalidDate = 3059,
        
        /// <summary>
        /// Geodatabase database does not have changes.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseDatabaseDoesNotHaveChanges = 3060,
        
        /// <summary>
        /// Geodatabase replica does not exist.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseReplicaDoesNotExist = 3061,
        
        /// <summary>
        /// Geodatabase storage type not supported.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseStorageTypeNotSupported = 3062,
        
        /// <summary>
        /// Geodatabase replica model error.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseReplicaModelError = 3063,
        
        /// <summary>
        /// Geodatabase replica client generation error.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseReplicaClientGenError = 3064,
        
        /// <summary>
        /// Geodatabase replica no upload to acknowledge.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseReplicaNoUploadToAcknowledge = 3065,
        
        /// <summary>
        /// Geodatabase last write time in the future.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseLastWriteTimeInTheFuture = 3066,
        
        /// <summary>
        /// Geodatabase invalid argument.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseInvalidArgument = 3067,
        
        /// <summary>
        /// Geodatabase transportation network corrupt.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseTransportationNetworkCorrupt = 3068,
        
        /// <summary>
        /// Geodatabase transportation network file IO error.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseTransportationNetworkFileIO = 3069,
        
        /// <summary>
        /// Geodatabase feature has pending edits.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseFeatureHasPendingEdits = 3070,
        
        /// <summary>
        /// Geodatabase change tracking not enabled.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseChangeTrackingNotEnabled = 3071,
        
        /// <summary>
        /// Geodatabase transportation network file open.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseTransportationNetworkFileOpen = 3072,
        
        /// <summary>
        /// Geodatabase transportation network unsupported.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseTransportationNetworkUnsupported = 3073,
        
        /// <summary>
        /// Geodatabase cannot sync copy.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseCannotSyncCopy = 3074,
        
        /// <summary>
        /// Geodatabase access control denied.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseAccessControlDenied = 3075,
        
        /// <summary>
        /// Geodatabase geometry outside replica extent.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseGeometryOutsideReplicaExtent = 3076,
        
        /// <summary>
        /// Geodatabase upload already in progress.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseUploadAlreadyInProgress = 3077,
        
        /// <summary>
        /// Geodatabase is closed.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseDatabaseClosed = 3078,
        
        /// <summary>
        /// Domain exists.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseDomainAlreadyExists = 3079,
        
        /// <summary>
        /// Geodatabase geometry type not supported.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseGeometryTypeNotSupported = 3080,
        
        /// <summary>
        /// ArcGISFeatureTable requires a global Id field to support adding bulk attachments.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseAttachmentsRequireGlobalIds = 3081,
        
        /// <summary>
        /// Violated attribute constraint rule.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseConstraintRuleViolation = 3082,
        
        /// <summary>
        /// The evaluation of attribute rules is cyclic or exceeds maximum cascading level.
        /// </summary>
        /// <since>1.0.0</since>
        GeodatabaseMaxRuleEvaluationLevelExceeded = 3083,
        
        /// <summary>
        /// The client version required by the geodatabase is not yet supported.
        /// </summary>
        /// <since>1.3.0</since>
        GeodatabaseUnsupportedVersion = 3084,
        
        /// <summary>
        /// Network attribute evaluator is not supported.
        /// </summary>
        /// <since>1.4.0</since>
        GeodatabaseTransportationNetworkUnsupportedEvaluator = 3085,
        
        /// <summary>
        /// Data changes cannot safely be committed due to mismatch of global ID support.
        /// </summary>
        /// <since>1.4.0</since>
        GeodatabaseGlobalIdSupportMismatch = 3086,
        
        /// <summary>
        /// Feature service does not support branch version reconciliation during sync.
        /// </summary>
        /// <since>1.6.0</since>
        GeodatabaseReconcileBranchVersionUnsupported = 3087,
        
        /// <summary>
        /// Geocode unsupported file format.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeUnsupportedFileFormat = 4001,
        
        /// <summary>
        /// Geocode unsupported spatial reference.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeUnsupportedSpatialReference = 4002,
        
        /// <summary>
        /// Geocode unsupported projection transformation.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeUnsupportedProjectionTransformation = 4003,
        
        /// <summary>
        /// Geocoder creation error.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeGeocoderCreation = 4004,
        
        /// <summary>
        /// Geocode intersections not supported.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeIntersectionsNotSupported = 4005,
        
        /// <summary>
        /// Uninitialized geocode task.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeUninitializedGeocodeTask = 4006,
        
        /// <summary>
        /// Invalid geocode locator properties.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeInvalidLocatorProperties = 4007,
        
        /// <summary>
        /// Geocode required field missing.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeRequiredFieldMissing = 4008,
        
        /// <summary>
        /// Geocode cannot read address.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeCannotReadAddress = 4009,
        
        /// <summary>
        /// Geocoding not supported.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeReverseGeocodingNotSupported = 4010,
        
        /// <summary>
        /// Geocode geometry type not supported.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeGeometryTypeNotSupported = 4011,
        
        /// <summary>
        /// Geocode suggest address not supported.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeSuggestAddressNotSupported = 4012,
        
        /// <summary>
        /// Geocode suggest result corrupt.
        /// </summary>
        /// <since>1.0.0</since>
        GeocodeSuggestResultCorrupted = 4013,
        
        /// <summary>
        /// Network analyst invalid route settings.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidRouteSettings = 5001,
        
        /// <summary>
        /// Network analyst no solution.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystNoSolution = 5002,
        
        /// <summary>
        /// Network analyst task canceled.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystTaskCanceled = 5003,
        
        /// <summary>
        /// Network analyst invalid network.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidNetwork = 5004,
        
        /// <summary>
        /// Network analyst directions error.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystDirectionsError = 5005,
        
        /// <summary>
        /// Network analyst insufficient number of stops.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInsufficientNumberOfStops = 5006,
        
        /// <summary>
        /// Network analyst stop unlocated.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystStopUnlocated = 5007,
        
        /// <summary>
        /// Network analyst stop located on non traversable element.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystStopLocatedOnNonTraversableElement = 5008,
        
        /// <summary>
        /// Network analyst point barrier invalid added cost attribute name.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystPointBarrierInvalidAddedCostAttributeName = 5009,
        
        /// <summary>
        /// Network analyst line barrier invalid scaled cost attribute name.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystLineBarrierInvalidScaledCostAttributeName = 5010,
        
        /// <summary>
        /// Network analyst polygon barrier invalid scaled cost attribute name.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystPolygonBarrierInvalidScaledCostAttributeName = 5011,
        
        /// <summary>
        /// Network analyst polygon barrier invalid scaled cost attribute value.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystPolygonBarrierInvalidScaledCostAttributeValue = 5012,
        
        /// <summary>
        /// Network analyst polyline barrier invalid scaled cost attribute value.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystPolylineBarrierInvalidScaledCostAttributeValue = 5013,
        
        /// <summary>
        /// Network analyst invalid impedance attribute.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidImpedanceAttribute = 5014,
        
        /// <summary>
        /// Network analyst invalid restriction attribute.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidRestrictionAttribute = 5015,
        
        /// <summary>
        /// Network analyst invalid accumulate attribute.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidAccumulateAttribute = 5016,
        
        /// <summary>
        /// Network analyst invalid directions time attribute.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidDirectionsTimeAttribute = 5017,
        
        /// <summary>
        /// Network analyst invalid directions distance attribute.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidDirectionsDistanceAttribute = 5018,
        
        /// <summary>
        /// Network analyst invalid attribute parameters attribute name.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidAttributeParametersAttributeName = 5019,
        
        /// <summary>
        /// Network analyst invalid attributes parameters parameter name.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidAttributeParametersParameterName = 5020,
        
        /// <summary>
        /// Network analyst invalid attributes parameters value type.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidAttributeParametersValueType = 5021,
        
        /// <summary>
        /// Network analyst invalid attribute parameters restriction usage value.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidAttributeParametersRestrictionUsageValue = 5022,
        
        /// <summary>
        /// Network analyst network has no hierarchy attribute.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystNetworkHasNoHierarchyAttribute = 5023,
        
        /// <summary>
        /// Network analyst no path found between stops.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystNoPathFoundBetweenStops = 5024,
        
        /// <summary>
        /// Network analyst undefined input spatial reference.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystUndefinedInputSpatialReference = 5025,
        
        /// <summary>
        /// Network analyst undefined output spatial reference.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystUndefinedOutputSpatialReference = 5026,
        
        /// <summary>
        /// Network analyst invalid directions style.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidDirectionsStyle = 5027,
        
        /// <summary>
        /// Deprecated. Network analyst invalid directions language.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidDirectionsLanguage = 5028,
        
        /// <summary>
        /// Network analyst directions time and impedance attribute mismatch.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystDirectionsTimeAndImpedanceAttributeMismatch = 5029,
        
        /// <summary>
        /// Network analyst invalid directions road class attribute.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidDirectionsRoadClassAttribute = 5030,
        
        /// <summary>
        /// Network analyst stop cannot be reached.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystStopIsUnreachable = 5031,
        
        /// <summary>
        /// Network analyst stop time window starts before unix epoch.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystStopTimeWindowStartsBeforeUnixEpoch = 5032,
        
        /// <summary>
        /// Network analyst stop time window is inverted.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystStopTimeWindowIsInverted = 5033,
        
        /// <summary>
        /// Walking mode route too large.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystWalkingModeRouteTooLarge = 5034,
        
        /// <summary>
        /// Stop has null geometry.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystStopHasNullGeometry = 5035,
        
        /// <summary>
        /// Point barrier has null geometry.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystPointBarrierHasNullGeometry = 5036,
        
        /// <summary>
        /// Polyline barrier has null geometry.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystPolylineBarrierHasNullGeometry = 5037,
        
        /// <summary>
        /// Polygon barrier has null geometry.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystPolygonBarrierHasNullGeometry = 5038,
        
        /// <summary>
        /// Online route task does not support search_where_clause condition.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystUnsupportedSearchWhereClause = 5039,
        
        /// <summary>
        /// Network analyst insufficient number of facilities.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInsufficientNumberOfFacilities = 5040,
        
        /// <summary>
        /// Network analyst facility has null geometry.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystFacilityHasNullGeometry = 5041,
        
        /// <summary>
        /// Network analyst facility has invalid added cost attribute name.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystFacilityHasInvalidAddedCostAttributeName = 5042,
        
        /// <summary>
        /// Network analyst facility has negative added cost attribute.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystFacilityHasNegativeAddedCostAttribute = 5043,
        
        /// <summary>
        /// Network analyst facility has invalid impedance cutoff.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystFacilityHasInvalidImpedanceCutoff = 5044,
        
        /// <summary>
        /// Network analyst insufficient number of incidents.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInsufficientNumberOfIncidents = 5045,
        
        /// <summary>
        /// Network analyst incident has null geometry.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystIncidentHasNullGeometry = 5046,
        
        /// <summary>
        /// Network analyst incident has invalid added cost attribute name.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystIncidentHasInvalidAddedCostAttributeName = 5047,
        
        /// <summary>
        /// Network analyst incident has negative added cost attribute.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystIncidentHasNegativeAddedCostAttribute = 5048,
        
        /// <summary>
        /// Network analyst invalid target facility count.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidTargetFacilityCount = 5049,
        
        /// <summary>
        /// Network analyst incident has invalid impedance cutoff.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystIncidentHasInvalidImpedanceCutoff = 5050,
        
        /// <summary>
        /// Network analyst start time is before Unix epoch.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystStartTimeIsBeforeUnixEpoch = 5051,
        
        /// <summary>
        /// Network analyst invalid default impedance cutoff.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidDefaultImpedanceCutoff = 5052,
        
        /// <summary>
        /// Network analyst invalid default target facility count.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidDefaultTargetFacilityCount = 5053,
        
        /// <summary>
        /// Network analyst invalid polygon buffer distance.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystInvalidPolygonBufferDistance = 5054,
        
        /// <summary>
        /// Network analyst polylines cannot be returned.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystPolylinesCannotBeReturned = 5055,
        
        /// <summary>
        /// Network analyst solving non time impedance, but time windows applied.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystTimeWindowsWithNonTimeImpedance = 5056,
        
        /// <summary>
        /// One or more stops have unsupported type.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystUnsupportedStopType = 5057,
        
        /// <summary>
        /// Network analyst route starts or ends on a waypoint.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystRouteStartsOrEndsOnWaypoint = 5058,
        
        /// <summary>
        /// Network analyst reordering stops (Traveling Salesman Problem) is not supported when the collection of stops contains waypoints or rest breaks.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystWaypointsAndRestBreaksForbiddenReordering = 5059,
        
        /// <summary>
        /// Network analyst waypoint contains time windows.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystWaypointHasTimeWindows = 5060,
        
        /// <summary>
        /// Network analyst waypoint contains added cost attribute.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystWaypointHasAddedCostAttribute = 5061,
        
        /// <summary>
        /// Network analyst stop has unknown curb approach.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystStopHasInvalidCurbApproach = 5062,
        
        /// <summary>
        /// Network analyst point barrier has unknown curb approach.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystPointBarrierHasInvalidCurbApproach = 5063,
        
        /// <summary>
        /// Network analyst facility has unknown curb approach.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystFacilityHasInvalidCurbApproach = 5064,
        
        /// <summary>
        /// Network analyst incident has unknown curb approach.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystIncidentHasInvalidCurbApproach = 5065,
        
        /// <summary>
        /// Network dataset has no directions attributes.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystNetworkDoesNotSupportDirections = 5066,
        
        /// <summary>
        /// Desired direction language not supported by platform.
        /// </summary>
        /// <since>1.0.0</since>
        NetworkAnalystDirectionsLanguageNotFound = 5067,
        
        /// <summary>
        /// Route result requires re-solving with current route task.
        /// </summary>
        NetworkAnalystRouteResultCannotBeUpdated = 5068,
        
        /// <summary>
        /// Input route result does not have directions.
        /// </summary>
        NetworkAnalystNoDirections = 5069,
        
        /// <summary>
        /// Input route result does not have stops.
        /// </summary>
        NetworkAnalystNoStops = 5070,
        
        /// <summary>
        /// Input route result doesn't have the route with passed route index.
        /// </summary>
        NetworkAnalystInvalidRouteIndex = 5071,
        
        /// <summary>
        /// Input remaining destinations count doesn't match with input routes stops collection.
        /// </summary>
        NetworkAnalystInvalidRemainingDestinationsCount = 5072,
        
        /// <summary>
        /// JSON parser invalid token.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidToken = 6001,
        
        /// <summary>
        /// JSON parser invalid character.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidCharacter = 6002,
        
        /// <summary>
        /// JSON parser invalid unicode.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidUnicode = 6003,
        
        /// <summary>
        /// JSON parser invalid start of JSON.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidJSONStart = 6004,
        
        /// <summary>
        /// JSON parser invalid end of pair.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidEndOfPair = 6005,
        
        /// <summary>
        /// JSON parser invalid end of element.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidEndOfElement = 6006,
        
        /// <summary>
        /// JSON parser invalid escape sequence.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidEscapeSequence = 6007,
        
        /// <summary>
        /// JSON parser invalid end of field name.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidEndOfFieldName = 6008,
        
        /// <summary>
        /// JSON parser invalid start of field name.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidStartOfFieldName = 6009,
        
        /// <summary>
        /// JSON parser invalid start of comment.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidStartOfComment = 6010,
        
        /// <summary>
        /// JSON parser invalid decimal digit.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidDecDigit = 6011,
        
        /// <summary>
        /// JSON parser invalid hex digit.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserInvalidHexDigit = 6012,
        
        /// <summary>
        /// JSON parser expecting null.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserExpectingNull = 6013,
        
        /// <summary>
        /// JSON parser expecting true.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserExpectingTrue = 6014,
        
        /// <summary>
        /// JSON parser expecting false.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserExpectingFalse = 6015,
        
        /// <summary>
        /// JSON parser expecting closing quote.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserExpectingClosingQuote = 6016,
        
        /// <summary>
        /// JSON parser expecting not a number.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserExpectingNan = 6017,
        
        /// <summary>
        /// JSON parser expecting end of comment.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserExpectingEndOfComment = 6018,
        
        /// <summary>
        /// JSON parser unexpected end of data.
        /// </summary>
        /// <since>1.0.0</since>
        JSONParserUnexpectedEndOfData = 6019,
        
        /// <summary>
        /// JSON object expecting start object.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectExpectingStartObject = 6020,
        
        /// <summary>
        /// JSON object expecting start array.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectExpectingStartArray = 6021,
        
        /// <summary>
        /// JSON object expecting value object.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectExpectingValueObject = 6022,
        
        /// <summary>
        /// JSON object expecting value array.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectExpectingValueArray = 6023,
        
        /// <summary>
        /// JSON object expecting value int32.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectExpectingValueInt32 = 6024,
        
        /// <summary>
        /// JSON object expecting integer type.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectExpectingIntegerType = 6025,
        
        /// <summary>
        /// JSON object expecting number type.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectExpectingNumberType = 6026,
        
        /// <summary>
        /// JSON object expecting value string.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectExpectingValueString = 6027,
        
        /// <summary>
        /// JSON object expecting value bool.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectExpectingValueBool = 6028,
        
        /// <summary>
        /// JSON object iterator not started.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectIteratorNotStarted = 6029,
        
        /// <summary>
        /// JSON object iterator is finished.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectIteratorIsFinished = 6030,
        
        /// <summary>
        /// JSON object key not found.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectKeyNotFound = 6031,
        
        /// <summary>
        /// JSON object index out of range.
        /// </summary>
        /// <since>1.0.0</since>
        JSONObjectIndexOutOfRange = 6032,
        
        /// <summary>
        /// JSON string writer JSON is complete.
        /// </summary>
        /// <since>1.0.0</since>
        JSONStringWriterJSONIsComplete = 6033,
        
        /// <summary>
        /// JSON string writer invalid JSON input.
        /// </summary>
        /// <since>1.0.0</since>
        JSONStringWriterInvalidJSONInput = 6034,
        
        /// <summary>
        /// JSON string writer expecting container.
        /// </summary>
        /// <since>1.0.0</since>
        JSONStringWriterExpectingContainer = 6035,
        
        /// <summary>
        /// JSON string writer expecting key or end object.
        /// </summary>
        /// <since>1.0.0</since>
        JSONStringWriterExpectingKeyOrEndObject = 6036,
        
        /// <summary>
        /// JSON string writer expecting value or end array.
        /// </summary>
        /// <since>1.0.0</since>
        JSONStringWriterExpectingValueOrEndArray = 6037,
        
        /// <summary>
        /// JSON string writer expecting value.
        /// </summary>
        /// <since>1.0.0</since>
        JSONStringWriterExpectingValue = 6038,
        
        /// <summary>
        /// Spatial reference is missing.
        /// </summary>
        /// <since>1.0.0</since>
        MappingMissingSpatialReference = 7001,
        
        /// <summary>
        /// Initial viewpoint is missing.
        /// </summary>
        /// <since>1.0.0</since>
        MappingMissingInitialViewpoint = 7002,
        
        /// <summary>
        /// Invalid request response.
        /// </summary>
        /// <since>1.0.0</since>
        MappingInvalidResponse = 7003,
        
        /// <summary>
        /// Bing maps key is missing.
        /// </summary>
        /// <since>1.0.0</since>
        MappingMissingBingMapsKey = 7004,
        
        /// <summary>
        /// Layer type is not supported.
        /// </summary>
        /// <since>1.0.0</since>
        MappingUnsupportedLayerType = 7005,
        
        /// <summary>
        /// Sync not enabled.
        /// </summary>
        /// <since>1.0.0</since>
        MappingSyncNotEnabled = 7006,
        
        /// <summary>
        /// Tile export is not enabled.
        /// </summary>
        /// <since>1.0.0</since>
        MappingTileExportNotEnabled = 7007,
        
        /// <summary>
        /// Required item property is missing.
        /// </summary>
        /// <since>1.0.0</since>
        MappingMissingItemProperty = 7008,
        
        /// <summary>
        /// Web map version is not supported.
        /// </summary>
        /// <since>1.0.0</since>
        MappingWebmapNotSupported = 7009,
        
        /// <summary>
        /// Spatial reference invalid or incompatible.
        /// </summary>
        /// <since>1.0.0</since>
        MappingSpatialReferenceInvalid = 7011,
        
        /// <summary>
        /// Package needs to be unpacked before it can be used.
        /// </summary>
        /// <since>1.0.0</since>
        MappingPackageUnpackRequired = 7012,
        
        /// <summary>
        /// Elevation source data format is not supported.
        /// </summary>
        /// <since>1.0.0</since>
        MappingUnsupportedElevationFormat = 7013,
        
        /// <summary>
        /// Web scene version or viewing mode is not supported.
        /// </summary>
        /// <since>1.0.0</since>
        MappingWebsceneNotSupported = 7014,
        
        /// <summary>
        /// Loadable object is not loaded when it is expected to be loaded.
        /// </summary>
        /// <since>1.0.0</since>
        MappingNotLoaded = 7015,
        
        /// <summary>
        /// Update packages for an offline map area are not supported.
        /// </summary>
        /// <since>1.0.0</since>
        MappingScheduledUpdatesNotSupported = 7016,
        
        /// <summary>
        /// Trace operation failed.
        /// </summary>
        /// <since>1.0.0</since>
        MappingUtilityNetworkTraceFailed = 7017,
        
        /// <summary>
        /// Arcade expression is invalid.
        /// </summary>
        /// <since>1.0.0</since>
        MappingInvalidArcadeExpression = 7018,
        
        /// <summary>
        /// Requested extent contains too many associations.
        /// </summary>
        /// <since>1.0.0</since>
        MappingUtilityNetworkTooManyAssociations = 7019,
        
        /// <summary>
        /// A layer has requested more features than the service maximum feature count.
        /// </summary>
        /// <since>1.0.0</since>
        MappingMaxFeatureCountExceeded = 7020,
        
        /// <summary>
        /// Feature service does not support branch versioning.
        /// </summary>
        /// <since>1.0.0</since>
        MappingBranchVersioningNotSupportedByService = 7021,
        
        /// <summary>
        /// Packaging of data for the offline map area is not complete and it is not ready for download.
        /// </summary>
        /// <since>1.0.0</since>
        MappingPackagingNotComplete = 7022,
        
        /// <summary>
        /// An upload sync direction is not supported.
        /// </summary>
        /// <since>1.0.0</since>
        MappingSyncDirectionUploadNotSupported = 7023,
        
        /// <summary>
        /// Tile export in .tpkx format is not supported.
        /// </summary>
        /// <since>1.0.0</since>
        MappingTileCacheCompactV2ExportNotEnabled = 7024,
        
        /// <summary>
        /// The specified layer does not intersect the area of interest.
        /// </summary>
        /// <since>1.0.0</since>
        MappingLayerDoesNotIntersectAreaOfInterest = 7025,
        
        /// <summary>
        /// Local edits must be sent to a service (using a sync direction of upload) before update packages can download a replacement geodatabase.
        /// </summary>
        /// <since>1.0.0</since>
        MappingScheduledUpdateUploadRequired = 7026,
        
        /// <summary>
        /// Unlicensed feature.
        /// </summary>
        /// <since>1.0.0</since>
        LicensingUnlicensedFeature = 8001,
        
        /// <summary>
        /// License level fixed.
        /// </summary>
        /// <since>1.0.0</since>
        LicensingLicenseLevelFixed = 8002,
        
        /// <summary>
        /// License level is already set.
        /// </summary>
        /// <since>1.0.0</since>
        LicensingLicenseLevelAlreadySet = 8003,
        
        /// <summary>
        /// Main license is not set.
        /// </summary>
        /// <since>1.0.0</since>
        LicensingMainLicenseNotSet = 8004,
        
        /// <summary>
        /// Unlicensed extension.
        /// </summary>
        /// <since>1.0.0</since>
        LicensingUnlicensedExtension = 8005,
        
        /// <summary>
        /// Portal user with no license.
        /// </summary>
        /// <since>1.0.0</since>
        LicensingPortalUserWithNoLicense = 8006,
        
        /// <summary>
        /// IO error.
        /// </summary>
        /// <since>1.0.0</since>
        StdIOSBaseFailure = 10001,
        
        /// <summary>
        /// Invalid array length.
        /// </summary>
        /// <since>1.0.0</since>
        StdBadArrayNewLength = 10002,
        
        /// <summary>
        /// Arithmetic underflow.
        /// </summary>
        /// <since>1.0.0</since>
        StdUnderflowError = 10003,
        
        /// <summary>
        /// System error.
        /// </summary>
        /// <since>1.0.0</since>
        StdSystemError = 10004,
        
        /// <summary>
        /// Range error.
        /// </summary>
        /// <since>1.0.0</since>
        StdRangeError = 10005,
        
        /// <summary>
        /// Arithmetic overflow.
        /// </summary>
        /// <since>1.0.0</since>
        StdOverflowError = 10006,
        
        /// <summary>
        /// Out of range.
        /// </summary>
        /// <since>1.0.0</since>
        StdOutOfRange = 10007,
        
        /// <summary>
        /// Length error.
        /// </summary>
        /// <since>1.0.0</since>
        StdLengthError = 10008,
        
        /// <summary>
        /// Invalid argument.
        /// </summary>
        /// <since>1.0.0</since>
        StdInvalidArgument = 10009,
        
        /// <summary>
        /// Asynchronous error.
        /// </summary>
        /// <since>1.0.0</since>
        StdFutureError = 10010,
        
        /// <summary>
        /// Math domain error.
        /// </summary>
        /// <since>1.0.0</since>
        StdDomainError = 10011,
        
        /// <summary>
        /// Unknown error.
        /// </summary>
        /// <since>1.0.0</since>
        StdRuntimeError = 10012,
        
        /// <summary>
        /// Logic error.
        /// </summary>
        /// <since>1.0.0</since>
        StdLogicError = 10013,
        
        /// <summary>
        /// Invalid weak reference.
        /// </summary>
        /// <since>1.0.0</since>
        StdBadWeakPtr = 10014,
        
        /// <summary>
        /// Invalid type Id.
        /// </summary>
        /// <since>1.0.0</since>
        StdBadTypeId = 10015,
        
        /// <summary>
        /// Invalid function call.
        /// </summary>
        /// <since>1.0.0</since>
        StdBadFunctionCall = 10016,
        
        /// <summary>
        /// Invalid error management.
        /// </summary>
        /// <since>1.0.0</since>
        StdBadException = 10017,
        
        /// <summary>
        /// Invalid cast.
        /// </summary>
        /// <since>1.0.0</since>
        StdBadCast = 10018,
        
        /// <summary>
        /// Out of memory.
        /// </summary>
        /// <since>1.0.0</since>
        StdBadAlloc = 10019,
        
        /// <summary>
        /// Unknown error.
        /// </summary>
        /// <since>1.0.0</since>
        StdException = 10020,
        
        /// <summary>
        /// Service doesn't support rerouting.
        /// </summary>
        /// <since>1.0.0</since>
        NavigationReroutingNotSupportedByService = 13001,
        
        /// <summary>
        /// HTTP client operation canceled.
        /// </summary>
        /// <since>1.0.0</since>
        HTTPClientOperationCanceled = 14001,
        
        /// <summary>
        /// HTTP client timed out.
        /// </summary>
        /// <since>1.0.0</since>
        HTTPClientTimedOut = 14002,
        
        /// <summary>
        /// HTTP client unsupported method.
        /// </summary>
        /// <since>1.0.0</since>
        HTTPClientUnsupportedMethod = 14003,
        
        /// <summary>
        /// HTTP client unsupported protocol scheme.
        /// </summary>
        /// <since>1.0.0</since>
        HTTPClientUnsupportedProtocolScheme = 14004,
        
        /// <summary>
        /// A problem was encountered with a <see cref="">GeotriggerFeed</see>.
        /// </summary>
        /// <remarks>
        /// An invalid <see cref="">GeotriggerFeed</see> indicates that a <see cref="">GeotriggerMonitor</see> is unable to
        /// perform checks. No <see cref="">GeotriggerNotificationInfo</see> events will be sent.
        /// </remarks>
        /// <since>1.0.0</since>
        GeotriggersFeedError = 16001,
        
        /// <summary>
        /// A problem was encountered with the <see cref="">FenceParameters</see> for a <see cref="">FenceGeotrigger</see>.
        /// </summary>
        /// <remarks>
        /// An invalid <see cref="">FenceParameters</see> indicates that a <see cref="">GeotriggerMonitor</see> is
        /// unable to perform checks. No <see cref="">GeotriggerNotificationInfo</see> events will be sent.
        /// </remarks>
        /// <since>1.0.0</since>
        GeotriggersFenceParametersError = 16002,
        
        /// <summary>
        /// A problem was encountered with the fence data for a <see cref="">Geotrigger</see>.
        /// </summary>
        /// <remarks>
        /// There is a problem with some of the fence data and these will not be checked by a
        /// <see cref="">GeotriggerMonitor</see>. However, other data is valid and so
        /// <see cref="">GeotriggerNotificationInfo</see> events can be sent.
        /// </remarks>
        /// <since>1.0.0</since>
        GeotriggersFenceDataWarning = 16003,
        
        /// <summary>
        /// Device doesn't support accelerometer.
        /// </summary>
        /// <since>1.0.0</since>
        MotionSensorAccelerometerNotSupported = 17000,
        
        /// <summary>
        /// Device doesn't support gyroscope.
        /// </summary>
        /// <since>1.0.0</since>
        MotionSensorGyroscopeNotSupported = 17001,
        
        /// <summary>
        /// Device doesn't support magnetometer.
        /// </summary>
        /// <since>1.0.0</since>
        MotionSensorMagnetometerNotSupported = 17002,
        
        /// <summary>
        /// Invalid credentials, unable to generate token.
        /// </summary>
        /// <since>1.1.0</since>
        AuthenticationInvalidCredentials = 18001,
        
        /// <summary>
        /// Unable to determine generate token URL.
        /// </summary>
        /// <since>1.1.0</since>
        AuthenticationUnableToDetermineTokenURL = 18002,
        
        /// <summary>
        /// Token has expired.
        /// </summary>
        /// <since>1.1.0</since>
        AuthenticationTokenExpired = 18003,
        
        /// <summary>
        /// A token or API key is required.
        /// </summary>
        /// <since>1.1.0</since>
        AuthenticationTokenRequired = 18004,
        
        /// <summary>
        /// Invalid API key.
        /// </summary>
        /// <since>1.1.0</since>
        AuthenticationInvalidAPIKey = 18005,
        
        /// <summary>
        /// Invalid token.
        /// </summary>
        /// <since>1.1.0</since>
        AuthenticationInvalidToken = 18006,
        
        /// <summary>
        /// You do not have permission to access the resource.
        /// </summary>
        /// <since>1.1.0</since>
        AuthenticationForbidden = 18007,
        
        /// <summary>
        /// Secure socket layer required.
        /// </summary>
        /// <since>1.1.0</since>
        AuthenticationSSLRequired = 18008,
        
        /// <summary>
        /// The credential cannot be shared with the request URL.
        /// </summary>
        /// <since>1.1.0</since>
        AuthenticationCredentialCannotBeShared = 18009,
        
        /// <summary>
        /// The authorization end point responded with a failure.
        /// </summary>
        /// <since>1.2.0</since>
        AuthenticationOAuthFailure = 18010,
        
        /// <summary>
        /// The authentication challenge was canceled.
        /// </summary>
        /// <since>1.3.0</since>
        AuthenticationChallengeCanceled = 18011,
        
        /// <summary>
        /// An error has occurred while creating an <see cref="">IAPCredential</see>.
        /// </summary>
        /// <since>2.1.0</since>
        AuthenticationIAPFailure = 18012,
        
        /// <summary>
        /// An <see cref="">IAPCredential</see> is required to access the resource.
        /// </summary>
        /// <since>2.1.0</since>
        AuthenticationIAPCredentialRequired = 18013,
        
        /// <summary>
        /// An <see cref="">IAPCredential</see> used to access the resource is no longer valid.
        /// </summary>
        /// <since>2.1.0</since>
        AuthenticationInvalidIAPCredential = 18014,
        
        /// <summary>
        /// Symbol dictionary not supported.
        /// </summary>
        /// <since>1.1.0</since>
        SymbolDictionaryNotSupported = 19000,
        
        /// <summary>
        /// Missing symbol dictionary configuration.
        /// </summary>
        /// <since>1.1.0</since>
        SymbolDictionaryMissingConfiguration = 19001,
        
        /// <summary>
        /// Missing symbol dictionary script.
        /// </summary>
        /// <since>1.1.0</since>
        SymbolDictionaryMissingScript = 19002,
        
        /// <summary>
        /// Missing symbol dictionary display name.
        /// </summary>
        /// <since>1.1.0</since>
        SymbolDictionaryMissingDisplayName = 19003,
        
        /// <summary>
        /// Error parsing the symbol dictionary configuration.
        /// </summary>
        /// <since>1.1.0</since>
        SymbolDictionaryConfigurationParsingError = 19004,
        
        /// <summary>
        /// Error parsing the symbol dictionary script.
        /// </summary>
        /// <since>1.1.0</since>
        SymbolDictionaryArcadeParsingError = 19005,
        
        /// <summary>
        /// Error evaluating the symbol dictionary script.
        /// </summary>
        /// <since>1.1.0</since>
        SymbolDictionaryArcadeEvaluationError = 19006,
        
        /// <summary>
        /// Error establishing connection with the stylx file for symbol dictionary.
        /// </summary>
        /// <since>1.1.0</since>
        SymbolDictionaryStylxConnectionError = 19007,
        
        /// <summary>
        /// Error converting ordered anchor points to geometry for symbol dictionary.
        /// </summary>
        /// <since>1.1.0</since>
        SymbolDictionaryOrderedAnchorPointToGeometryError = 19008,
        
        /// <summary>
        /// Error converting geometry to ordered anchor points for symbol dictionary.
        /// </summary>
        /// <since>1.1.0</since>
        SymbolDictionaryGeometryToOrderedAnchorPointError = 19009,
        
        /// <summary>
        /// The Field is not nullable and cannot be set to null.
        /// </summary>
        /// <since>1.5.0</since>
        FeatureFormNullNotAllowedError = 20000,
        
        /// <summary>
        /// Field value exceeds the value of <see cref="">DateTimePickerFormInput.max</see>.
        /// </summary>
        /// <since>1.5.0</since>
        FeatureFormExceedsMaximumDateTimeError = 20001,
        
        /// <summary>
        /// Field value is less than the value of <see cref="">DateTimePickerFormInput.min</see>.
        /// </summary>
        /// <since>1.5.0</since>
        FeatureFormLessThanMinimumDateTimeError = 20002,
        
        /// <summary>
        /// Field value exceeds the maximum allowed length specified by the <see cref="">FormInput</see>.
        /// </summary>
        /// <since>1.5.0</since>
        FeatureFormExceedsMaximumLengthError = 20003,
        
        /// <summary>
        /// Field value is less than the minimum allowed length specified by the <see cref="">FormInput</see>.
        /// </summary>
        /// <since>1.5.0</since>
        FeatureFormLessThanMinimumLengthError = 20004,
        
        /// <summary>
        /// Field value exceeds the allowed numeric maximum specified by <see cref="">FieldFormElement.domain</see>.
        /// </summary>
        /// <since>1.5.0</since>
        FeatureFormExceedsNumericMaximumError = 20005,
        
        /// <summary>
        /// Field value is less than the allowed numeric minimum specified by <see cref="">FieldFormElement.domain</see>.
        /// </summary>
        /// <since>1.5.0</since>
        FeatureFormLessThanNumericMinimumError = 20006,
        
        /// <summary>
        /// Field value is not present in the coded value domain.
        /// </summary>
        /// <since>1.5.0</since>
        FeatureFormNotInCodedValueDomainError = 20007,
        
        /// <summary>
        /// Field value is required and the <see cref="">FieldFormElement.value</see> is null or empty.
        /// </summary>
        /// <since>1.5.0</since>
        FeatureFormFieldIsRequiredError = 20008,
        
        /// <summary>
        /// The type of the <see cref="">FieldFormElement.value</see> does not match the type of the field.
        /// </summary>
        /// <since>1.5.0</since>
        FeatureFormIncorrectValueTypeError = 20009
    };
}