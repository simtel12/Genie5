using System.Windows.Input;

namespace Genie.App.ViewModels;

/// <summary>
/// One entry in the Edit → Theme submenu (#20): display text (custom themes
/// get a "(Custom)" suffix), the theme's real name for lookup, whether it is
/// the active theme (drives the radio check), and a pre-bound
/// <see cref="ICommand"/> so the menu can bind without an ancestor cast —
/// same shape as <see cref="LayoutMenuItem"/>.
/// </summary>
public sealed record ThemeMenuItem(string Display, string Name, bool IsCurrent, ICommand Command);
