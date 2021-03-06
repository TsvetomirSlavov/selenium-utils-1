﻿using OpenQA.Selenium;
using Riganti.Utils.Testing.Selenium.Core.Exceptions;
using System;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;
using System.Threading;

namespace Riganti.Utils.Testing.Selenium.Core
{
    public class BrowserWrapper
    {
        // ReSharper disable once InconsistentNaming
        protected readonly IWebDriver browser;

        private readonly ITestBase testClass;
        public int ActionWaitTime { get; set; } = SeleniumTestsConfiguration.ActionTimeout;
        public string BaseUrl { get; set; } = SeleniumTestsConfiguration.BaseUrl;

        public IWebDriver Browser
        {
            get
            {
                ActivateScope();
                return browser;
            }
        }

        private ScopeOptions ScopeOptions { get; set; }

        public BrowserWrapper(IWebDriver browser, ITestBase testClass, ScopeOptions scope)
        {
            this.browser = browser;
            this.testClass = testClass;
            ScopeOptions = scope;
            SetCssSelector();
        }

        public void SetTimeouts(TimeSpan pageLoadTimeout, TimeSpan implicitlyWait)
        {
            var timeouts = browser.Manage().Timeouts();
            timeouts.SetPageLoadTimeout(pageLoadTimeout);
            timeouts.ImplicitlyWait(implicitlyWait);
        }

        private Func<string, By> selectMethodFunc;

        public virtual Func<string, By> SelectMethod
        {
            get { return selectMethodFunc; }
            set
            {
                if (value == null)
                { throw new ArgumentException("SelectMethod cannot be null. This method is used to select elements from loaded page."); }
                selectMethodFunc = value;
            }
        }

        public void SetCssSelector()
        {
            selectMethodFunc = By.CssSelector;
        }

        /// <summary>
        /// Url of active browser tab.
        /// </summary>
        public string CurrentUrl => Browser.Url;

        /// <summary>
        /// Gives path of url of active browser tab.
        /// </summary>
        public string CurrentUrlPath => new Uri(CurrentUrl).GetLeftPart(UriPartial.Path);

        /// <summary>
        /// Compares url with current url of browser.
        /// </summary>
        public bool CompareUrl(string url)
        {
            Uri uri1 = new Uri(url);
            Uri uri2 = new Uri(Browser.Url);

            var result = Uri.Compare(uri1, uri2,
                UriComponents.Scheme | UriComponents.Host | UriComponents.PathAndQuery,
                UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase);

            return result == 0;
        }

        /// <summary>
        /// Compates current Url and given url.
        /// </summary>
        /// <param name="url">This url is compared with CurrentUrl.</param>
        /// <param name="urlKind">Determine whether url parameter contains relative or absolute path.</param>
        /// <param name="components">Determine what parts of urls are compared.</param>
        public bool CompareUrl(string url, UrlKind urlKind, params UriComponents[] components)
        {
            var currentUri = new Uri(CurrentUrl);
            //support relative domain
            //(new Uri() cannot parse the url correctly when the host is missing
            if (urlKind == UrlKind.Relative)
            {
                url = url.StartsWith("/") ? $"{currentUri.Scheme}://{currentUri.Host}{url}" : $"{currentUri.Scheme}://{currentUri.Host}/{url}";
            }

            if (urlKind == UrlKind.Absolute && url.StartsWith("//"))
            {
                if (!string.IsNullOrWhiteSpace(currentUri.Scheme))
                {
                    url = currentUri.Scheme + ":" + url;
                }
            }

            var expectedUri = new Uri(url, UriKind.Absolute);

            if (components.Length == 0)
            {
                throw new BrowserLocationException($"Function CheckUrlCheckUrl(string, UriKind, params UriComponents) has to have one UriComponents at least.");
            }
            UriComponents finalComponent = components[0];
            components.ToList().ForEach(s => finalComponent |= s);

            return Uri.Compare(currentUri, expectedUri, finalComponent, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// Clicks on element.
        /// </summary>
        public BrowserWrapper Click(string selector)
        {
            First(selector).Click();
            Wait();
            return this;
        }

        /// <summary>
        /// Submits this element to the web server.
        /// </summary>
        /// <remarks>
        /// If this current element is a form, or an element within a form,
        ///             then this will be submitted to the web server. If this causes the current
        ///             page to change, then this method will block until the new page is loaded.
        /// </remarks>
        public BrowserWrapper Submit(string selector)
        {
            First(selector).Submit();
            Wait();
            return this;
        }

        /// <summary>
        /// Navigates to specific url.
        /// </summary>
        /// <param name="url">url to navigate </param>
        /// <remarks>
        /// If url is ABSOLUTE, browser is navigated directly to url.
        /// If url is RELATIVE, browser is navigated to url combined from base url and relative url.
        /// Base url is specified in test configuration. (This is NOT url host of current page!)
        /// </remarks>
        public void NavigateToUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                if (string.IsNullOrWhiteSpace(BaseUrl))
                {
                    throw new InvalidRedirectException();
                }
                SeleniumTestBase.Log($"Start navigation to: {BaseUrl}", 10);
                Browser.Navigate().GoToUrl(BaseUrl);
                return;
            }
            //redirect if is absolute
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                SeleniumTestBase.Log($"Start navigation to: {url}", 10);
                Browser.Navigate().GoToUrl(url);
                return;
            }
            //redirect absolute with same schema
            if (url.StartsWith("//"))
            {
                var schema = new Uri(CurrentUrl).Scheme;
                var navigateUrltmp = $"{schema}:{url}";
                SeleniumTestBase.Log($"Start navigation to: {navigateUrltmp}", 10);
                Browser.Navigate().GoToUrl(navigateUrltmp);
                return;
            }
            var builder = new UriBuilder(BaseUrl);

            // replace url fragments
            if (url.StartsWith("/"))
            {
                builder.Path = "";
                var urlToNavigate = builder.ToString().TrimEnd('/') + "/" + url.TrimStart('/');
                SeleniumTestBase.Log($"Start navigation to: {urlToNavigate}", 10);
                Browser.Navigate().GoToUrl(urlToNavigate);
                return;
            }

            var navigateUrl = builder.ToString().TrimEnd('/') + "/" + url.TrimStart('/');
            SeleniumTestBase.Log($"Start navigation to: {navigateUrl}", 10);
            Browser.Navigate().GoToUrl(navigateUrl);
        }

        /// <summary>
        /// Redirects to base url specified in test configuration
        /// </summary>
        public void NavigateToUrl()
        {
            NavigateToUrl(null);
        }

        /// <summary>
        /// Redirects to page back in Browser history
        /// </summary>
        public void NavigateBack()
        {
            Browser.Navigate().Back();
        }

        /// <summary>
        /// Redirects to page forward in Browser history
        /// </summary>
        public void NavigateForward()
        {
            Browser.Navigate().Forward();
        }

        /// <summary>
        /// Reloads current page.
        /// </summary>
        public void Refresh()
        {
            Browser.Navigate().Refresh();
        }

        /// <summary>
        /// Forcibly ends test.
        /// </summary>
        /// <param name="message">Test failure message</param>
        public void DropTest(string message)
        {
            throw new WebDriverException($"Test forcibly dropped: {message}");
        }

        public string GetAlertText()
        {
            var alert = GetAlert();
            return alert?.Text;
        }

        public BrowserWrapper CheckIfAlertTextEquals(string expectedValue, bool caseSensitive = false, bool trim = true)
        {
            var alert = GetAlert();
            var alertText = "";
            if (trim)
            {
                alertText = alert.Text?.Trim();
                expectedValue = expectedValue.Trim();
            }

            if (!string.Equals(alertText, expectedValue,
                    caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            {
                throw new AlertException($"Alert does not contain expected value. Expected value: '{expectedValue}', provided value: '{alertText}'");
            }
            return this;
        }

        public bool HasAlert()
        {
            try
            {
                GetAlert();
            }
            catch
            {
                return false;
            }
            return true;
        }

        public IAlert GetAlert()
        {
            IAlert alert;
            try

            {
                alert = Browser.SwitchTo().Alert();
            }
            catch (Exception ex)
            {
                throw new AlertException("Alert not visible.", ex);
            }
            if (alert == null)
                throw new AlertException("Alert not visible.");
            return alert;
        }

        /// <summary>
        /// Checks if modal dialog (Alert) contains specified text as a part of provided text from the dialog.
        /// </summary>
        public BrowserWrapper CheckIfAlertTextContains(string expectedValue, bool trim = true)
        {
            var alert = GetAlert();
            var alertText = "";
            if (trim)
            {
                alertText = alert.Text?.Trim();
                expectedValue = expectedValue.Trim();
            }

            if (alertText == null || !alertText.Contains(expectedValue))
            {
                throw new AlertException($"Alert does not contain expected value. Expected value: '{expectedValue}', provided value: '{alertText}'");
            }
            return this;
        }

        /// <summary>
        /// Checks if modal dialog (Alert) text equals with specified text.
        /// </summary>
        public BrowserWrapper CheckIfAlertText(Func<string, bool> expression, string failureMessage = "")
        {
            var alert = Browser.SwitchTo().Alert()?.Text;
            if (!expression(alert))
            {
                throw new AlertException($"Alert text is not correct. Provided value: '{alert}' \n { failureMessage } ");
            }
            return this;
        }

        /// <summary>
        /// Confirms modal dialog (Alert).
        /// </summary>
        public BrowserWrapper ConfirmAlert()
        {
            Browser.SwitchTo().Alert().Accept();
            Wait();
            return this;
        }

        /// <summary>
        /// Dismisses modal dialog (Alert).
        /// </summary>
        public BrowserWrapper DismissAlert()
        {
            Browser.SwitchTo().Alert().Dismiss();
            Wait();
            return this;
        }

        /// <summary>
        /// Waits specified time in milliseconds.
        /// </summary>
        public BrowserWrapper Wait(int milliseconds)
        {
            Thread.Sleep(milliseconds);
            return this;
        }

        /// <summary>
        /// Waits time specified by ActionWaitType property.
        /// </summary>
        public BrowserWrapper Wait()
        {
            return Wait(ActionWaitTime);
        }

        /// <summary>
        /// Waits specified time.
        /// </summary>
        public BrowserWrapper Wait(TimeSpan interval)
        {
            Thread.Sleep((int)interval.TotalMilliseconds);
            return this;
        }

        /// <summary>
        /// Finds all elements that satisfy the condition of css selector.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        public ElementWrapperCollection FindElements(By selector)
        {
            return Browser.FindElements(selector).ToElementsList(this, selector.GetSelector());
        }

        /// <summary>
        /// Finds all elements that satisfy the condition of css selector.
        /// </summary>
        /// <param name="cssSelector"></param>
        /// <returns></returns>
        public ElementWrapperCollection FindElements(string cssSelector, Func<string, By> tmpSelectMethod = null)
        {
            return Browser.FindElements((tmpSelectMethod ?? SelectMethod)(cssSelector)).ToElementsList(this, (tmpSelectMethod ?? SelectMethod)(cssSelector).GetSelector());
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public ElementWrapper FirstOrDefault(string selector, Func<string, By> tmpSelectMethod = null)
        {
            var elms = FindElements(selector, tmpSelectMethod);
            return elms.FirstOrDefault();
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public ElementWrapper First(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return ThrowIfIsNull(FirstOrDefault(selector, tmpSelectMethod), $"Element not found. Selector: {selector}");
        }

        public BrowserWrapper ForEach(string selector, Action<ElementWrapper> action, Func<string, By> tmpSelectMethod = null)
        {
            FindElements(selector, tmpSelectMethod).ForEach(action);
            return this;
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public ElementWrapper SingleOrDefault(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).SingleOrDefault();
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public ElementWrapper Single(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).Single();
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public bool IsDisplayed(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).All(s => s.IsDisplayed());
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public ElementWrapperCollection CheckIfIsDisplayed(string selector, Func<string, By> tmpSelectMethod = null)
        {
            var collection = FindElements(selector, tmpSelectMethod);
            var result = collection.All(s => s.IsDisplayed());
            if (!result)
            {
                var index = collection.IndexOf(collection.First(s => !s.IsDisplayed()));
                throw new UnexpectedElementStateException($"One or more elements are not displayed. Selector '{selector}', Index of non-displayed element: {index}");
            }
            return collection;
        }

        ///<summary>Provides elements that satisfies the selector condition at specific position.</summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>
        public ElementWrapperCollection CheckIfIsNotDisplayed(string selector, Func<string, By> tmpSelectMethod = null)
        {
            var collection = FindElements(selector, tmpSelectMethod);
            var result = collection.All(s => s.IsDisplayed()) && collection.Any();
            if (result)
            {
                var index = collection.Any() ? collection.IndexOf(collection.First(s => !s.IsDisplayed())) : -1;
                throw new UnexpectedElementStateException($"One or more elements are displayed and they shouldn't be. Selector '{selector}', Index of non-displayed element: {index}");
            }
            return collection;
        }

        ///<summary>Provides elements that satisfies the selector condition at specific position.</summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public ElementWrapper ElementAt(string selector, int index, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).ElementAt(index);
        }

        ///<summary>Provides the last element that satisfies the selector condition.</summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public ElementWrapper Last(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).Last();
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>
        public ElementWrapper LastOrDefault(string selector, Func<string, By> tmpSelectMethod = null)
        {
            return FindElements(selector, tmpSelectMethod).LastOrDefault();
        }

        public BrowserWrapper FireJsBlur()
        {
            GetJavaScriptExecutor()?.ExecuteScript("if(document.activeElement && document.activeElement.blur) {document.activeElement.blur()}");
            return this;
        }

        public IJavaScriptExecutor GetJavaScriptExecutor()
        {
            return Browser as IJavaScriptExecutor;
        }

        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>

        public BrowserWrapper SendKeys(string selector, string text, Func<string, By> tmpSelectMethod = null)
        {
            FindElements(selector, tmpSelectMethod).ForEach(s => { s.SendKeys(text); s.Wait(); });
            return this;
        }

        /// <summary>
        /// Removes content from selected elements
        /// </summary>
        /// <param name="tmpSelectMethod">temporary method which determine how the elements are selected</param>
        public BrowserWrapper ClearElementsContent(string selector, Func<string, By> tmpSelectMethod = null)
        {
            FindElements(selector, tmpSelectMethod).ForEach(s => { s.Clear(); s.Wait(); });
            return this;
        }

        /// <summary>
        /// Throws exception when provided object is null
        /// </summary>
        /// <param name="obj">Tested object</param>
        /// <param name="message">Failure message</param>
        public T ThrowIfIsNull<T>(T obj, string message)
        {
            if (obj == null)
            {
                throw new NoSuchElementException(message);
            }
            return obj;
        }

        /// <summary>
        /// Takes a screenshot and returns a full path to the file.
        /// </summary>
        ///<param name="filename">Path where the screenshot is going to be saved.</param>
        ///<param name="format">Default value is PNG.</param>
        public void TakeScreenshot(string filename, ImageFormat format = null)
        {
            ((ITakesScreenshot)Browser).GetScreenshot().SaveAsFile(filename, format ?? ImageFormat.Png);
        }

        /// <summary>
        /// Closes the current browser
        /// </summary>
        public void Dispose()
        {
            Browser.Quit();
            Browser.Dispose();
            if (Browser is IWebDriverWrapper)
            {
                ((IWebDriverWrapper)Browser).Disposed = true;
                SeleniumTestBase.LogDriverId(Browser, "Dispose - ChromeDriver");
            }

            SeleniumTestBase.Log("IWebDriver was disposed.");
        }

        #region CheckUrl

        /// <summary>
        /// Checks exact match with CurrentUrl
        /// </summary>
        /// <param name="url">This url is compared with CurrentUrl.</param>
        public BrowserWrapper CheckUrlEquals(string url)
        {
            var uri1 = new Uri(CurrentUrl, UriKind.Absolute);
            var uri2 = new Uri(url, UriKind.RelativeOrAbsolute);
            if (uri1 != uri2)
            {
                throw new BrowserLocationException($"Current url is not expected. Current url: '{CurrentUrl}', Expected url: '{url}'.");
            }
            return this;
        }

        /// <summary>
        /// Checks if CurrentUrl satisfies the condition defined by lamda expression
        /// </summary>
        /// <param name="expression">The condition</param>
        /// <param name="failureMessage">Failure message</param>
        public BrowserWrapper CheckUrl(Func<string, bool> expression, string failureMessage = null)
        {
            if (!expression(CurrentUrl))
            {
                throw new BrowserLocationException($"Current url is not expected. Current url: '{CurrentUrl}'. " + (failureMessage ?? ""));
            }
            return this;
        }

        /// <summary>
        /// Checks url by its parts
        /// </summary>
        /// <param name="url">This url is compared with CurrentUrl.</param>
        /// <param name="urlKind">Determine whether url parameter contains relative or absolute path.</param>
        /// <param name="components">Determine what parts of urls are compared.</param>
        public BrowserWrapper CheckUrl(string url, UrlKind urlKind, params UriComponents[] components)
        {
            if (!CompareUrl(url, urlKind, components))
            {
                throw new BrowserLocationException($"Current url is not expected. Current url: '{CurrentUrl}'. Expected url: '{url}'");
            }
            return this;
        }

        #endregion CheckUrl

        #region FileUploadDialog

        /// <summary>
        /// Opens file dialog and sends keys with full path to file, that should be uploaded.
        /// </summary>
        /// <param name="fileUploadOpener">Element that opens file dialog after it is clicked.</param>
        /// <param name="fullFileName">Full path to file that is intended to be uploaded.</param>
        public virtual BrowserWrapper FileUploadDialogSelect(ElementWrapper fileUploadOpener, string fullFileName)
        {
            if (fileUploadOpener.GetTagName() == "input" && fileUploadOpener.HasAttribute("type") && fileUploadOpener.GetAttribute("type") == "file")
            {
                fileUploadOpener.SendKeys(fullFileName);
                Wait();
            }
            else
            {
                // open file dialog
                fileUploadOpener.Click();
                Wait();
                //Another wait is needed because without it sometimes few chars from file path are skipped.
                Wait(1000);
                // write the full path to the dialog
                System.Windows.Forms.SendKeys.SendWait(fullFileName);
                Wait();
                SendEnterKey();
            }
            return this;
        }

        public virtual BrowserWrapper SendEnterKey()
        {
            System.Windows.Forms.SendKeys.SendWait("{Enter}");
            Wait();
            return this;
        }

        public virtual BrowserWrapper SendEscKey()
        {
            System.Windows.Forms.SendKeys.SendWait("{ESC}");
            Wait();
            return this;
        }

        #endregion FileUploadDialog

        #region Frames support

        public BrowserWrapper GetFrameScope(string selector)
        {
            var options = new ScopeOptions { FrameSelector = selector, Parent = this, CurrentWindowHandle = browser.CurrentWindowHandle };

            var iframe = First(selector);
            iframe.CheckIfTagName(new[] { "iframe", "frame" }, $"The selected element '{iframe.FullSelector}' is not a iframe element.");
            var frame = browser.SwitchTo().Frame(iframe.WebElement);
            testClass.ActiveScope = options.ScopeId;
            return new BrowserWrapper(frame, testClass, options);
        }

        #endregion Frames support

        public BrowserWrapper CheckIfHyperLinkEquals(string selector, string url, UrlKind kind, params UriComponents[] components)
        {
            ForEach(selector, element =>
                {
                    element.CheckIfHyperLinkEquals(url, kind, components);
                });
            return this;
        }

        /// <summary>
        /// Waits until the condition is true.
        /// </summary>
        /// <param name="condition">Expression that determine whether test should wait or continue</param>
        /// <param name="maxTimeout">If condition is not reached in this timeout (ms) test is dropped.</param>
        /// <param name="failureMessage">Message which is displayed in exception log in case that the condition is not reached</param>
        /// <param name="ignoreCertainException">When StaleElementReferenceException or InvalidElementStateException is thrown than it would be ignored.</param>
        /// <param name="checkInterval">Interval in miliseconds. RECOMMENDATION: let the interval greater than 250ms</param>
        public BrowserWrapper WaitFor(Func<bool> condition, int maxTimeout, string failureMessage, bool ignoreCertainException = true, int checkInterval = 500)
        {
            if (condition == null)
            {
                throw new NullReferenceException("Condition cannot be null.");
            }
            var now = DateTime.UtcNow;

            bool isConditionMet = false;
            Exception ex = null;
            do
            {
                try
                {
                    isConditionMet = condition();
                }
                catch (StaleElementReferenceException)
                {
                    if (!ignoreCertainException)
                        throw;
                }
                catch (InvalidElementStateException)
                {
                    if (!ignoreCertainException)
                        throw;
                }

                if (DateTime.UtcNow.Subtract(now).TotalMilliseconds > maxTimeout)
                {
                    throw new WaitBlockException(failureMessage);
                }
                Wait(checkInterval);
            } while (!isConditionMet);
            return this;
        }

        /// <summary>
        /// Repeats execution of the action until the action is executed without exception.
        /// </summary>
        /// <param name="maxTimeout">If condition is not reached in this timeout (ms) test is dropped.</param>
        /// <param name="checkInterval">Interval in miliseconds. RECOMMENDATION: let the interval greater than 250ms</param>
        public BrowserWrapper WaitFor(Action action, int maxTimeout, int checkInterval = 500, string failureMessage = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
            var now = DateTime.UtcNow;

            Exception exceptionThrown = null;
            do
            {
                try
                {
                    action();
                    exceptionThrown = null;
                }
                catch (Exception ex)
                {
                    exceptionThrown = ex;
                }

                if (DateTime.UtcNow.Subtract(now).TotalMilliseconds > maxTimeout)
                {
                    if (failureMessage != null)
                    {
                        throw new WaitBlockException(failureMessage, exceptionThrown);
                    }
                    throw exceptionThrown;
                }
                Wait(checkInterval);
            } while (exceptionThrown != null);
            return this;
        }

        /// <summary>
        /// Checks if browser can access given Url (browser returns status code 2??).
        /// </summary>
        /// <param name="url"></param>
        /// <param name="urlKind"></param>
        /// <returns></returns>
        public BrowserWrapper CheckIfUrlIsAccessible(string url, UrlKind urlKind)
        {
            var currentUri = new Uri(CurrentUrl);

            if (urlKind == UrlKind.Relative)
            {
                url = GetAbsoluteUrl(url);
            }

            if (urlKind == UrlKind.Absolute && url.StartsWith("//"))
            {
                if (!string.IsNullOrWhiteSpace(currentUri.Scheme))
                {
                    url = currentUri.Scheme + ":" + url;
                }
            }

            HttpWebResponse response = null;
            SeleniumTestBase.Log($"CheckIfUrlIsAccessible: Checking of url: '{url}'", 10);
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "HEAD";

            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException e)
            {
                throw new WebException($"Unable to access {url}! {e.Status}", e);
            }
            finally
            {
                response?.Close();
            }
            return this;
        }

        /// <summary>
        /// Transforms relative Url to absolute. Uses base URL.
        /// </summary>
        /// <param name="relativeUrl"></param>
        /// <returns></returns>
        public string GetAbsoluteUrl(string relativeUrl)
        {
            var currentUri = new Uri(BaseUrl);
            return relativeUrl.StartsWith("/") ? $"{currentUri.Scheme}://{currentUri.Host}:{currentUri.Port}{relativeUrl}" : $"{currentUri.Scheme}://{currentUri.Host}:{currentUri.Port}/{relativeUrl}";
        }

        /// <summary>
        /// Switches browser tabs.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public BrowserWrapper SwitchToTab(int index)
        {
            Browser.SwitchTo().Window(Browser.WindowHandles[index]);
            return this;
        }
        
        public void ActivateScope()
        {
            if (testClass.ActiveScope == ScopeOptions.ScopeId)
            {
                return;
            }
            if (ScopeOptions.Parent != null && ScopeOptions.Parent != this)
            {
                ScopeOptions.Parent.ActivateScope();
            }
            else
            {
                if (ScopeOptions.CurrentWindowHandle != null && browser.CurrentWindowHandle != ScopeOptions.CurrentWindowHandle)
                {
                    browser.SwitchTo().Window(ScopeOptions.CurrentWindowHandle);
                }
                if (ScopeOptions.Parent == null)
                {
                    browser.SwitchTo().DefaultContent();
                }

                if (ScopeOptions.FrameSelector != null)
                {
                    browser.SwitchTo().Frame(ScopeOptions.FrameSelector);
                }
            }
            testClass.ActiveScope = ScopeOptions.ScopeId;
        }

        public string GetTitle() => Browser.Title;

        public BrowserWrapper CheckIfTitleEquals(string title, StringComparison comparison = StringComparison.OrdinalIgnoreCase, bool trim = true)
        {
            var browserTitle = GetTitle();
            if (trim)
            {
                browserTitle = browserTitle.Trim();
                title = title.Trim();
            }

            if (!string.Equals(title, browserTitle, comparison))
            {
                throw new BrowserException($"Provided content in tab's title is not expected. Expected value: '{title}', provided value: '{browserTitle}'");
            }
            return this;
        }

        public BrowserWrapper CheckIfTitleNotEquals(string title, StringComparison comparison = StringComparison.OrdinalIgnoreCase, bool trim = true)
        {
            var browserTitle = GetTitle();
            if (trim)
            {
                browserTitle = browserTitle.Trim();
                title = title.Trim();
            }

            if (string.Equals(title, browserTitle, comparison))
            {
                throw new BrowserException($"Provided content in tab's title is not expected. Title should NOT to be equal to '{title}', but provided value is '{browserTitle}'");
            }
            return this;
        }

        public BrowserWrapper CheckIfTitle(Func<string, bool> func, string failureMessage = "")
        {
            var browserTitle = GetTitle();

            if (!func(browserTitle))
            {
                throw new BrowserException($"Provided content in tab's title is not expected. Provided content: '{browserTitle}' \r\n{failureMessage}");
            }
            return this;
        }

        /// <summary>
        /// Returns WebDriver withnout scope activation. Be carefull!!! This is unsecure!
        /// </summary>
        public IWebDriver _GetInternalWebDriver()
        {
            testClass.ActiveScope = Guid.Empty;
            return browser;
        }
    }
}