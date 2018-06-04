using System;
using WebScraper.Lib;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.PhantomJS;
using OpenQA.Selenium.Support.PageObjects;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using OpenQA.Selenium.Interactions;

namespace WebScraper.Flysas
{

    public class WebDriverFlysas
    {
        private static readonly Uri homepageUri = new Uri("https://www.flysas.com/en/");
        private static readonly string domain = "www.flysas.com";
        private IStorage disk = new RoundTripDataFileStorage();
        private IWebDriver webDriver;

        //setup browser
        public WebDriverFlysas()
        {
            var service = ChromeDriverService.CreateDefaultService(@"bin/Debug/netcoreapp2.0/", "chromedriver.exe");
            var options = new ChromeOptions();

            options.AddArgument("--headless");
            options.AddExtension(Path.GetFullPath("GA_Opt-out.crx"));

            webDriver = new ChromeDriver(service, options);
            webDriver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(2); //wait settings
        }

        public void StartScrape(QueryOptions query)
        {
            webDriver.Navigate().GoToUrl(homepageUri);          //get initial page
            var main = new MainPageObjectModel(webDriver);
            var jsExecutor = webDriver as IJavaScriptExecutor;    //get jsexecutor for the page

            jsExecutor.ExecuteScript("hideMarketSelector()");

            // select trip type
            if (query.IsRoundTrip)
                main.TripTypeSelectors.First(t => t.GetAttribute("value") == "roundtrip").Click();
            else
                main.TripTypeSelectors.First(t => t.GetAttribute("value") == "oneway").Click();

            // fill in arrival and departure airports
            main.DepartureField.SendKeys(query.Departure);
            webDriver.FindElement(By.Id($"{query.Departure}")).Click();

            main.ArrivalField.SendKeys(query.Arrival);
            webDriver.FindElement(By.Id($"{query.Arrival}")).Click();

            // fill out the dates
            main.FillCalendar(main.FlightOutDateBttn, query.DepDate);
            main.FillCalendar(main.FlightInDateBttn, query.RetDate);

            jsExecutor.ExecuteScript("arguments[0].click()", main.SearchButton);

            // Actions submitAction = new Actions(webDriver);
            // submitAction.MoveToElement(main.SearchButton).Click().Perform();

            // main.SearchButton.Click();

            // wait for the flight table page to load
            // get data from the page?

            // disk.SaveCollectedData(collectedData);

            string source = webDriver.PageSource;
            webDriver.Quit();
        }
    }

    public class MainPageObjectModel
    {
        private IWebDriver driver;

        public MainPageObjectModel(IWebDriver driver) => this.driver = driver;

        public ReadOnlyCollection<IWebElement> TripTypeSelectors =>
            driver.FindElements(By.Name("ctl00$FullRegion$MainRegion$ContentRegion$ContentFullRegion$ContentLeftRegion$CEPGroup1$CEPActive$cepNDPRevBookingArea$ceptravelTypeSelector$TripTypeSelector"));

        public IWebElement DepartureField =>
            driver.FindElement(By.Id("ctl00_FullRegion_MainRegion_ContentRegion_ContentFullRegion_ContentLeftRegion_CEPGroup1_CEPActive_cepNDPRevBookingArea_predictiveSearch_txtFrom"));

        public IWebElement ArrivalField =>
            driver.FindElement(By.Id("ctl00_FullRegion_MainRegion_ContentRegion_ContentFullRegion_ContentLeftRegion_CEPGroup1_CEPActive_cepNDPRevBookingArea_predictiveSearch_txtTo"));

        public IWebElement SearchButton =>
            driver.FindElement(By.Id("ctl00_FullRegion_MainRegion_ContentRegion_ContentFullRegion_ContentLeftRegion_CEPGroup1_CEPActive_cepNDPRevBookingArea_Searchbtn_ButtonLink"));

        public IWebElement FlightOutDateBttn =>
            driver.FindElement(By.XPath("//*[@class='flOutDate hasDatepicker']"));

        public IWebElement FlightInDateBttn =>
            driver.FindElement(By.XPath("//*[@class='flInDate hasDatepicker']"));

        public IWebElement SelectedMonth =>
            driver.FindElement(By.XPath("//*[@class='ui-datepicker-month']"));

        public ReadOnlyCollection<IWebElement> MonthOptions =>
            driver.FindElements(By.XPath("//*[@class='ui-datepicker-month-link']"));

        public ReadOnlyCollection<IWebElement> DayOptions =>
            driver.FindElements(By.XPath("//a[@class='ui-state-default']"));

    }
    public static class MainPageUtils
    {
        public static void FillCalendar(this MainPageObjectModel page, IWebElement dateBttn, DateTime date)
        {
            dateBttn.Click();

            if (page.SelectedMonth.Text.ToUpper() != date.ToString("MMMM").ToUpper())
                page.MonthOptions.First(option => option.Text.ToUpper() == date.ToString("MMM").ToUpper()).Click();
            // else page.SelectedMonth.Click();
            page.DayOptions.First(option => option.Text == date.Day.ToString()).Click();
        }
       
    }
}