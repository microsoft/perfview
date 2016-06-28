namespace TraceEventAPIServer.Extensions
{
    using System;
    using System.Collections.Specialized;
    using System.Linq;
    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.Routing;

    public static class HtmlExtensions
    {
        public static string Json(this IHtmlHelper html, object obj)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
        }

        public static HtmlString MyRouteLink(this IHtmlHelper htmlHelper, string linkText, string routeName, object routeValues, string[] excludeUrlKeys)
        {
            UrlHelper urlHelper = new UrlHelper(htmlHelper.ViewContext);
            var request = htmlHelper.ViewContext.HttpContext.Request;

            UriBuilder uriBuilder = new UriBuilder(urlHelper.RouteUrl(routeName, routeValues, request.Scheme));
            NameValueCollection nameValueCollection1 = new NameValueCollection();

            foreach (string index in request.Query.Keys)
            {
                if (!excludeUrlKeys.Contains(index))
                {
                    nameValueCollection1[index] = request.Query[index];
                }
            }

            var nameValueCollection2 = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uriBuilder.Query);
            foreach (string index in nameValueCollection2.Keys)
            {
                nameValueCollection1[index] = nameValueCollection2[index];
            }

            uriBuilder.Query = nameValueCollection1.ToString();

            TagBuilder tagBuilder = new TagBuilder("a");
            tagBuilder.Attributes.Add(new System.Collections.Generic.KeyValuePair<string, string>("href", uriBuilder.ToString()));
            tagBuilder.InnerHtml.SetContent(linkText);
            return new HtmlString(tagBuilder.ToString());
        }

        public static HtmlString MyRouteLinkTargetTab(this IHtmlHelper htmlHelper, string linkText, string routeName, object routeValues, string[] excludeUrlKeys)
        {
            UrlHelper urlHelper = new UrlHelper(htmlHelper.ViewContext);
            var request = htmlHelper.ViewContext.HttpContext.Request;

            UriBuilder uriBuilder = new UriBuilder(urlHelper.RouteUrl(routeName, routeValues, request.Scheme));
            NameValueCollection nameValueCollection1 = new NameValueCollection();

            foreach (string index in request.Query.Keys)
            {
                if (!excludeUrlKeys.Contains(index))
                {
                    nameValueCollection1[index] = request.Query[index];
                }
            }
            
            var nameValueCollection2 = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uriBuilder.Query);
            foreach (string index in nameValueCollection2.Keys)
            {
                nameValueCollection1[index] = nameValueCollection2[index];
            }

            uriBuilder.Query = nameValueCollection1.ToString();

            TagBuilder tagBuilder = new TagBuilder("a");
            tagBuilder.Attributes.Add(new System.Collections.Generic.KeyValuePair<string, string>("href", uriBuilder.ToString()));
            tagBuilder.Attributes.Add(new System.Collections.Generic.KeyValuePair<string, string>("target", "_blank"));
            tagBuilder.InnerHtml.SetContent(linkText);
            return new HtmlString(tagBuilder.ToString());
        }

        public static string MyRouteLinkAjax(this IHtmlHelper htmlHelper)
        {
            return htmlHelper.ViewContext.HttpContext.Request.QueryString.ToString();
        }
    }
}