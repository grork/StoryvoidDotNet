using Codevoid.Storyvoid.Pages;
using Codevoid.Utilities.OAuth;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Dispatching;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Codevoid.Storyvoid.Utilities;

internal record NavigationParameter(object? Parameter, IAppUtilities Utilities);

interface IAppUtilities
{
    /// <summary>
    /// Show the login page, allowing someone to enter user + password
    /// </summary>
    void ShowLogin();

    /// <summary>
    /// Shows the Article List
    /// </summary>
    void ShowList();

    /// <summary>
    /// Shows a placeholder page; the parameter is displayed as the result of a
    /// call to `ToString()` on the parameter instance.
    /// </summary>
    /// <param name="parameter">Optional parameter</param>
    void ShowPlaceholder(object? parameter = null);
}

/// <summary>
/// Utility Class to allow navigations to be decoupled from the views.
/// </summary>
internal class AppUtilities: IAppUtilities
{
    /// <summary>
    /// Count to help differentiate different non-parameterized placeholder
    /// pages
    /// </summary>
    private static int placeholderCount = 1;

    private Frame frame;

    internal AppUtilities(Frame frame)
    {
        this.frame = frame;
    }

    /// <inheritdoc />
    public void ShowLogin() => this.ShowPlaceholder("Login");

    /// <inheritdoc/>
    public void ShowList()
    {
        this.ShowPlaceholder("List");
    }

    /// <inheritdoc/>
    public void ShowPlaceholder(object? parameter = null)
    {
        var navigationParameter = new NavigationParameter((parameter == null) ? placeholderCount++ : parameter, this);
        this.frame.Navigate(typeof(PlaceholderPage), navigationParameter);
    }
}
