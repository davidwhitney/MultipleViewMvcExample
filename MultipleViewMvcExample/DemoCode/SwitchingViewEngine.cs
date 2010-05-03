using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace MultipleViewMvcExample.DemoCode
{
    public class SwitchingViewEngine : WebFormViewEngine
    {
        private const string CacheKeyFormat = ":ViewCacheEntry:{0}:{1}:{2}:{3}:";
        private const string CacheKeyPrefixMaster = "Master";
        private const string CacheKeyPrefixView = "View";
        private static readonly List<string> EmptyLocations = new List<string>();

        protected string[] MobileViewLocationFormats { get; private set; }
        protected string[] MobileMasterLocationFormats { get; private set; }

        public SwitchingViewEngine()
        {
            ViewLocationFormats = new[]
                                      {
                                          "~/Views/{1}/{0}.aspx", "~/Views/{1}/{0}.ascx", "~/Views/Shared/{0}.aspx",
                                          "~/Views/Shared/{0}.ascx"
                                      };

            MobileViewLocationFormats = new[]
                                            {
                                                "~/Views/{1}/{0}.mobile.aspx", "~/Views/{1}/{0}.mobile.ascx",
                                                "~/Views/Shared/{0}.mobile.aspx",
                                                "~/Views/Shared/{0}.mobile.ascx"
                                            };

            MasterLocationFormats = new[] {"~/Views/{1}/{0}.master", "~/Views/Shared/{0}.master"};
            MobileMasterLocationFormats = new[] {"~/Views/{1}/{0}.mobile.master", "~/Views/Shared/{0}.mobile.master"};
        }

        public override ViewEngineResult FindView(ControllerContext controllerContext, string viewName, string masterName, bool useCache)
        {
            if (controllerContext == null)
            {
                throw new ArgumentNullException("controllerContext");
            }
            if (String.IsNullOrEmpty(viewName))
            {
                throw new ArgumentException("viewName");
            }

            List<string> viewLocationsSearched;
            List<string> masterLocationsSearched;

            string[] viewLocationsToSearch = ViewLocationFormats;
            string[] masterLocationsToSearch = MasterLocationFormats;

            viewLocationsToSearch = AddMobileViewLocations(controllerContext, viewLocationsToSearch, MobileViewLocationFormats);
            masterLocationsToSearch = AddMobileViewLocations(controllerContext, masterLocationsToSearch, MobileMasterLocationFormats);

            string controllerName = controllerContext.RouteData.GetRequiredString("controller");
            string viewPath = GetPath(controllerContext, viewLocationsToSearch, viewName, controllerName, CacheKeyPrefixView, useCache, out viewLocationsSearched);
            string masterPath = GetPath(controllerContext, masterLocationsToSearch, masterName, controllerName, CacheKeyPrefixMaster, useCache, out masterLocationsSearched);

            if (String.IsNullOrEmpty(viewPath) 
                || (String.IsNullOrEmpty(masterPath) 
                && !String.IsNullOrEmpty(masterName)))
            {
                return new ViewEngineResult(viewLocationsSearched.Union(masterLocationsSearched));
            }

            return new ViewEngineResult(CreateView(controllerContext, viewPath, masterPath), this);
        }

        private static string[] AddMobileViewLocations(ControllerContext controllerContext,
                                                       string[] viewLocationsToSearch,
                                                       IEnumerable<string> mobileViewLocations)
        {
            if (controllerContext == null
                || controllerContext.HttpContext == null
                || controllerContext.HttpContext.Request == null
                || controllerContext.HttpContext.Request.Browser == null
                || viewLocationsToSearch == null
                || viewLocationsToSearch.Length == 0
                || mobileViewLocations == null
                || mobileViewLocations.ToList().Count == 0
                || !controllerContext.HttpContext.Request.Browser.IsMobileDevice)
            {
                return viewLocationsToSearch;
            }

            var mobileViews = viewLocationsToSearch.ToList();
            foreach (var view in mobileViewLocations.Reverse())
            {
                mobileViews.Insert(0, view);
            }

            viewLocationsToSearch = mobileViews.ToArray();

            return viewLocationsToSearch;
        }

        private string GetPath(ControllerContext controllerContext, string[] locations, string name,
                               string controllerName, string cacheKeyPrefix, bool useCache,
                               out List<string> searchedLocations)
        {
            searchedLocations = EmptyLocations;
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            if ((locations == null) || (locations.Length == 0))
            {
                throw new InvalidOperationException("Property cannot be null or empty.");
            }
            bool flag = IsSpecificPath(name);
            string key = CreateCacheKey(cacheKeyPrefix, name, flag ? string.Empty : controllerName);
            if (useCache)
            {
                string viewLocation = ViewLocationCache.GetViewLocation(controllerContext.HttpContext, key);
                if (viewLocation != null)
                {
                    return viewLocation;
                }
            }
            if (!flag)
            {
                return GetPathFromGeneralName(controllerContext, locations, name, controllerName, key,ref searchedLocations);
            }
            return GetPathFromSpecificName(controllerContext, name, key, ref searchedLocations);
        }
        
        private static bool IsSpecificPath(string name)
        {
            char ch = name[0];
            if (ch != '~')
            {
                return (ch == '/');
            }
            return true;
        }

        private string GetPathFromSpecificName(ControllerContext controllerContext, string name, string cacheKey,
                                               ref List<string> searchedLocations)
        {
            string virtualPath = name;
            if (!FileExists(controllerContext, name))
            {
                virtualPath = string.Empty;
                searchedLocations = new List<string> {name};
            }
            ViewLocationCache.InsertViewLocation(controllerContext.HttpContext, cacheKey, virtualPath);
            return virtualPath;
        }

        private string GetPathFromGeneralName(ControllerContext controllerContext, string[] locations, string name,
                                              string controllerName, string cacheKey, ref List<string> searchedLocations)
        {
            string virtualPath = string.Empty;
            searchedLocations = new List<string>();
            for (int i = 0; i < locations.Length; i++)
            {
                string str2 = string.Format(CultureInfo.InvariantCulture, locations[i],
                                            new object[] {name, controllerName});
                if (FileExists(controllerContext, str2))
                {
                    searchedLocations = EmptyLocations;
                    virtualPath = str2;
                    ViewLocationCache.InsertViewLocation(controllerContext.HttpContext, cacheKey, virtualPath);
                    return virtualPath;
                }
                searchedLocations[i] = str2;
            }
            return virtualPath;
        }

        private string CreateCacheKey(string prefix, string name, string controllerName)
        {
            return String.Format(CultureInfo.InvariantCulture, CacheKeyFormat,
                                 GetType().AssemblyQualifiedName, prefix, name, controllerName);
        }
    }
}