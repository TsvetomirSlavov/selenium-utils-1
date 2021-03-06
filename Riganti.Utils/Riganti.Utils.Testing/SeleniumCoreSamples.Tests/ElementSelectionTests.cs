﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Riganti.Utils.Testing.Selenium.Core;

namespace WebApplication1.Tests
{
    [TestClass]
    public class ElementSelectionTests : SeleniumTestBase
    {
        [TestMethod]
        public void ElementChildrenSelection()
        {
            RunInAllBrowsers(browser =>
            {
                browser.NavigateToUrl("ElementSelectionTest.aspx");

                var elm = browser.First("#top-element");
                Assert.AreEqual(elm.Children.Count, 4);
            });
        }
    }
}