using Codevoid.Storyvoid.Pages;

namespace Codevoid.Storyvoid.Utilities;

/// <summary>
/// Utility Class to allow navigations to be decoupled from the views.
/// </summary>
internal class AppUtilities
{
    /// <summary>
    /// Count to help differentiate different non-parameterized placeholder
    /// pages
    /// </summary>
    private static int placeholderCount = 1;

    private Frame frame;

    internal AppUtilities(Frame frame) => this.frame = frame;

    /// <summary>
    /// Show the login page, allowing someone to enter user + password
    /// </summary>
    internal void ShowLogin() => this.ShowPlaceholder("Login");

    /// <summary>
    /// Shows the Article List
    /// </summary>
    internal void ShowList() => this.ShowPlaceholder("List");

    /// <summary>
    /// Shows a placeholder page; the parameter is displayed as the result of a
    /// call to `ToString()` on the parameter instance.
    /// </summary>
    /// <param name="parameter">Optional parameter</param>
    internal void ShowPlaceholder(object? parameter = null) => this.frame.Navigate(typeof(PlaceholderPage), (parameter == null) ? placeholderCount++ : parameter);
}
