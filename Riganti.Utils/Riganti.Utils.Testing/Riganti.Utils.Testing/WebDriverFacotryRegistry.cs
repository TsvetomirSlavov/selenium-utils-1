﻿using OpenQA.Selenium;
using System;
using System.Collections.Generic;

namespace Riganti.Utils.Testing.Selenium.Core
{
    public class WebDriverFactoryRegistry
    {
        public WebDriverFactoryRegistry()
        {
            BrowserFactories = new List<IWebDriverFactory>();
            if (SeleniumTestsConfiguration.StartChromeDriver) RegisterBrowserFactory(new DefaultChromeWebDriverFactory());
            if (SeleniumTestsConfiguration.StartInternetExplorerDriver) RegisterBrowserFactory(new DefaultInternetExplorerWebDriverFactory());
            if (SeleniumTestsConfiguration.StartFirefoxDriver) RegisterBrowserFactory(new DefaultFirefoxWebDriverFactory());
        }

        public List<IWebDriverFactory> BrowserFactories { get; }

        public void RegisterBrowserFactoryMethod(Func<IWebDriver> func)
        {
            BrowserFactories.Add(new WebDriverFactoryMethodWrapper(func));
        }

        public void RegisterBrowserFactory(IWebDriverFactory factory)
        {
            BrowserFactories.Add(factory);
        }

        public void Clear()
        {
            BrowserFactories.Clear();
        }
    }
}