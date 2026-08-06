using PerfView;
using PerfViewTests.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Xunit;
using Xunit.Abstractions;

namespace PerfViewTests
{
    public class MainWindowTests : PerfViewTestBase
    {
        public MainWindowTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [WpfFact]
        public Task TabNavigationReachesMainContentBeforeStatusBarAsync()
        {
            return RunUITestAsync(
                () => Task.FromResult(GuiApp.MainWindow),
                async window =>
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync();

                    Grid root = Assert.IsType<Grid>(window.Content);
                    Grid mainContent = root.Children
                        .OfType<Grid>()
                        .Single(child => Grid.GetRow(child) == 1);
                    Hyperlink videosLink = window.VideoLink.Inlines
                        .OfType<Hyperlink>()
                        .Single();

                    Assert.Same(videosLink, Keyboard.Focus(videosLink));

                    var visitedElements = new HashSet<IInputElement> { videosLink };
                    while (!mainContent.IsKeyboardFocusWithin)
                    {
                        Assert.True(MoveFocusToNextElement());
                        Assert.False(window.StatusBar.IsKeyboardFocusWithin);

                        IInputElement focusedElement = Keyboard.FocusedElement;
                        Assert.NotNull(focusedElement);
                        Assert.True(
                            visitedElements.Add(focusedElement),
                            "Focus cycled without reaching the main content.");
                    }

                    Assert.True(mainContent.IsKeyboardFocusWithin);
                },
                window => Task.CompletedTask);
        }

        #region private

        private static bool MoveFocusToNextElement()
        {
            var request = new TraversalRequest(FocusNavigationDirection.Next);
            IInputElement focusedElement = Keyboard.FocusedElement;

            if (focusedElement is UIElement uiElement)
            {
                return uiElement.MoveFocus(request);
            }

            if (focusedElement is ContentElement contentElement)
            {
                return contentElement.MoveFocus(request);
            }

            return false;
        }

        #endregion
    }
}
