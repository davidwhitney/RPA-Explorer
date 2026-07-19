using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace RPA_Explorer
{
    // Minimal replacement for System.Windows.Forms.MessageBox.
    public static class MessageBox
    {
        public static Task ShowInfo(Window owner, string message, string title)
            => Show(owner, message, title, false);

        public static Task ShowError(Window owner, string message, string title)
            => Show(owner, message, title, false);

        // Returns true for "Yes".
        public static async Task<bool> ShowYesNo(Window owner, string message, string title)
        {
            return await Show(owner, message, title, true) == true;
        }

        private static async Task<bool?> Show(Window owner, string message, string title, bool yesNo)
        {
            bool? result = null;

            Window dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                MinWidth = 320,
                MaxWidth = 640,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false
            };

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8
            };

            if (yesNo)
            {
                Button yes = new Button { Content = "Yes", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
                Button no = new Button { Content = "No", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
                yes.Click += (_, _) => { result = true; dialog.Close(); };
                no.Click += (_, _) => { result = false; dialog.Close(); };
                buttons.Children.Add(yes);
                buttons.Children.Add(no);
            }
            else
            {
                Button ok = new Button { Content = "OK", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
                ok.Click += (_, _) => { result = true; dialog.Close(); };
                buttons.Children.Add(ok);
            }

            dialog.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 600
                    },
                    buttons
                }
            };

            if (owner != null)
            {
                await dialog.ShowDialog(owner);
            }
            else
            {
                dialog.Show();
            }

            return result;
        }
    }
}
