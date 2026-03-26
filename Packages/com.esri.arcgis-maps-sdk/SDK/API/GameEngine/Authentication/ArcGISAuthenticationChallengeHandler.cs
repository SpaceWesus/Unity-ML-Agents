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
namespace Esri.GameEngine.Authentication
{
	/// <summary>
	/// An interface for handling ArcGIS authentication challenges.
	/// </summary>
	/// <since>1.1.0</since>
	public partial interface ArcGISAuthenticationChallengeHandler
	{
		#region Methods
		/// <summary>
		/// Handles the given authentication challenge.
		/// </summary>
		/// <remarks>
		/// Use <see cref="GameEngine.Authentication.ArcGISAuthenticationChallenge.ContinueWithCredential">ArcGISAuthenticationChallenge.ContinueWithCredential</see>, <see cref="GameEngine.Authentication.ArcGISAuthenticationChallenge.ContinueAndFail">ArcGISAuthenticationChallenge.ContinueAndFail</see>, or
		/// <see cref="GameEngine.Authentication.ArcGISAuthenticationChallenge.Cancel">ArcGISAuthenticationChallenge.Cancel</see> to handle the challenge.
		/// The credential provided while handling an authentication challenge is placed in the ArcGIS credential store of the
		/// <see cref="GameEngine.ArcGISRuntimeEnvironment.AuthenticationManager">ArcGISRuntimeEnvironment.AuthenticationManager</see> and used by all subsequent requests that have a matching server context.
		/// </remarks>
		/// <param name="challenge">The challenge to be handled.</param>
		/// <seealso cref="GameEngine.Authentication.ArcGISCredential.ServerContext">ArcGISCredential.ServerContext</seealso>
		/// <since>1.1.0</since>
		void HandleArcGISAuthenticationChallenge(ArcGISAuthenticationChallenge challenge);
		#endregion Methods
	}
}
