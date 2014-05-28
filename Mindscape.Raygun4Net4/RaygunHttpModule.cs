﻿using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Mindscape.Raygun4Net
{
  public class RaygunHttpModule : IHttpModule
  {
    private bool ExcludeErrorsBasedOnHttpStatusCode { get; set; }
    private bool ExcludeErrorsFromLocal { get; set; }

    private int[] HttpStatusCodesToExclude { get; set; }

    public void Init(HttpApplication context)
    {
      if (GlobalFilters.Filters.Count == 1)
      {
        Filter filter = GlobalFilters.Filters.FirstOrDefault();
        if (filter.Instance is HandleErrorAttribute)
        {
          GlobalFilters.Filters.Add(new RaygunExceptionFilterAttribute());
        }
      }
      else
      {
        if (!HasRaygunFilter)
        {
          context.Error += SendError;
          HttpStatusCodesToExclude = string.IsNullOrEmpty(RaygunSettings.Settings.ExcludeHttpStatusCodesList) ? new int[0] : RaygunSettings.Settings.ExcludeHttpStatusCodesList.Split(',').Select(int.Parse).ToArray();
          ExcludeErrorsBasedOnHttpStatusCode = HttpStatusCodesToExclude.Any();
          ExcludeErrorsFromLocal = RaygunSettings.Settings.ExcludeErrorsFromLocal;
        }
      }
    }

    private static bool HasRaygunFilter
    {
      get
      {
        foreach (Filter filter in GlobalFilters.Filters)
        {
          if (filter.Instance is RaygunExceptionFilterAttribute)
          {
            return true;
          }
        }
        return false;
      }
    }

    public void Dispose()
    {
    }

    private void SendError(object sender, EventArgs e)
    {
      var application = (HttpApplication)sender;
      var lastError = application.Server.GetLastError();

      if (CanSend(lastError))
      {
        new RaygunClient().SendInBackground(Unwrap(lastError));
      }
    }

    public bool CanSend(Exception exception)
    {
      if (ExcludeErrorsBasedOnHttpStatusCode && exception is HttpException && HttpStatusCodesToExclude.Contains(((HttpException)exception).GetHttpCode()))
      {
        return false;
      }

      if (ExcludeErrorsFromLocal && HttpContext.Current.Request.IsLocal)
      {
        return false;
      }

      return true;
    }

    private Exception Unwrap(Exception exception)
    {
      if (exception is HttpUnhandledException)
      {
        return exception.GetBaseException();
      }

      return exception;
    }
  }
}