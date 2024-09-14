﻿using Microsoft.SharePoint.Client;
using Newtonsoft.Json;
using NovaPointLibrary.Commands.SharePoint.Item;
using NovaPointLibrary.Commands.SharePoint.List;
using NovaPointLibrary.Commands.Utilities.RESTModel;
using NovaPointLibrary.Commands.Utilities;
using System.Linq.Expressions;
using static NovaPointLibrary.Commands.SharePoint.Permision.SPOSharingLinksREST;
using PnP.Core.Model.SharePoint;
using NovaPointLibrary.Commands.SharePoint.Site;


namespace NovaPointLibrary.Solutions.Report
{
    public class ListReport : ISolution
    {
        public static readonly string s_SolutionName = "Libraries and Lists report";
        public static readonly string s_SolutionDocs = "https://github.com/Barbarur/NovaPoint/wiki/Solution-Report-ListReport";

        private ListReportParameters _param;
        private readonly NPLogger _logger;
        private readonly Commands.Authentication.AppInfo _appInfo;

        private static readonly Expression<Func<Microsoft.SharePoint.Client.List, object>>[] _listExpresions = new Expression<Func<Microsoft.SharePoint.Client.List, object>>[]
        {
            l => l.Hidden,

            l => l.BaseType,
            l => l.Title,
            l => l.DefaultViewUrl,
            l => l.Id,

            l => l.Created,
            l => l.LastItemUserModifiedDate,

            l => l.ItemCount,

            l => l.RootFolder,
            l => l.RootFolder.StorageMetrics,
            l => l.RootFolder.StorageMetrics.LastModified,
            l => l.RootFolder.StorageMetrics.TotalFileCount,
            l => l.RootFolder.StorageMetrics.TotalFileStreamSize,
            l => l.RootFolder.StorageMetrics.TotalSize,

            l => l.EnableVersioning,
            l => l.MajorVersionLimit,
            l => l.EnableMinorVersions,
            l => l.MajorWithMinorVersionsLimit,

            l => l.IrmEnabled,

            l => l.ForceCheckout,

            l => l.EnableModeration,
        };

        private ListReport(NPLogger logger, Commands.Authentication.AppInfo appInfo, ListReportParameters parameters)
        {
            _param = parameters;
            _logger = logger;
            _appInfo = appInfo;
        }

        public static async Task RunAsync(ListReportParameters parameters, Action<LogInfo> uiAddLog, CancellationTokenSource cancelTokenSource)
        {
            parameters.ListsParam.ListExpresions = _listExpresions;

            NPLogger logger = new(uiAddLog, "ListReport", parameters);

            try
            {
                Commands.Authentication.AppInfo appInfo = await Commands.Authentication.AppInfo.BuildAsync(logger, cancelTokenSource);

                await new ListReport(logger, appInfo, parameters).RunScriptAsync();

                logger.ScriptFinish();

            }
            catch (Exception ex)
            {
                logger.ScriptFinish(ex);
            }
        }

        private async Task RunScriptAsync()
        {
            _appInfo.IsCancelled();

            await foreach (var listRecord in new SPOTenantListsCSOM(_logger, _appInfo, _param.TListsParam).GetAsync())
            {
                if (listRecord.Ex != null)
                {
                    AddRecord(new(listRecord.SiteUrl, listRecord.List, listRecord.Ex.Message));
                    continue;
                }

                try
                {
                    await ProcessList(listRecord.SiteUrl, listRecord.List);
                }
                catch (Exception ex)
                {
                    _logger.ReportError(GetType().Name, "Site", listRecord.SiteUrl, ex);
                    AddRecord(new(listRecord.SiteUrl, listRecord.List, ex.Message));
                }

            }
        }
        
        private async Task ProcessList(string siteUrl, List list)
        {
            _appInfo.IsCancelled();

            ListReportRecord record = new(siteUrl, list);

            if (_param.IncludeStorageMetrics)
            {
                var storageMetricsResponse = await new SPOFolderCSOM(_logger, _appInfo).GetFolderStorageMetricAsync(siteUrl, list.RootFolder);
                record.AddStorageMetrics(storageMetricsResponse.StorageMetrics);
            }

            AddRecord(record);
        }

        private void AddRecord(ListReportRecord record)
        {
            _logger.RecordCSV(record);
        }

    }

    public class ListReportRecord : ISolutionRecord
    {
        internal string SiteURL { get; set; }

        internal string ListTitle { get; set; } = String.Empty;
        internal string ListType { get; set; } = String.Empty;
        internal string ListServerRelativeUrl { get; set; } = String.Empty;
        internal string ListID { get; set; } = String.Empty;

        internal string Created { get; set; } = String.Empty;
        internal string LastModified { get; set; } = String.Empty;
        internal string TotalFileCount { get; set; } = String.Empty;
        internal string TotalSizeGb { get; set; } = String.Empty;

        internal string ContentApproval { get; set; } = String.Empty;
        internal string MajorVersioning { get; set; } = String.Empty;
        internal string MajorVersionLimit { get; set; } = String.Empty;
        internal string MinorVersioning { get; set; } = String.Empty;
        internal string MinorVersionLimit { get; set; } = String.Empty;
        internal string RequireCheckOut { get; set; } = String.Empty;

        internal string IRM_Emabled { get; set; } = String.Empty;

        internal string Hidden { get; set; } = String.Empty;
        internal string IsSystemList { get; set; } = String.Empty;
        
        internal string Remarks { get; set; }

        internal ListReportRecord(string siteUrl, List? list = null, string remarks = "")
        {
            SiteURL = siteUrl;
            Remarks = remarks;

            if (list != null)
            {
                ListTitle = list.Title;
                ListType = list.BaseType.ToString();
                ListServerRelativeUrl = list.RootFolder.ServerRelativeUrl;
                ListID = list.Id.ToString();

                Created = list.Created.ToString();
                LastModified = list.LastItemUserModifiedDate.ToString();
                TotalFileCount = list.ItemCount.ToString();

                ContentApproval = list.EnableModeration.ToString();
                MajorVersioning = list.EnableVersioning.ToString();
                MajorVersionLimit = list.MajorVersionLimit.ToString();
                MinorVersioning = list.EnableMinorVersions.ToString();
                MinorVersionLimit = list.MajorWithMinorVersionsLimit.ToString();
                RequireCheckOut = list.ForceCheckout.ToString();

                IRM_Emabled = list.IrmEnabled.ToString();

                Hidden = list.Hidden.ToString();
                IsSystemList = list.IsSystemList.ToString();
            }
        }

        internal void AddStorageMetrics(RESTStorageMetrics storageMetrics)
        {
            TotalSizeGb = Math.Round(storageMetrics.TotalSize / Math.Pow(1024, 3), 2).ToString();
        }

    }

    public class ListReportParameters : ISolutionParameters
    {
        public bool IncludeStorageMetrics { get; set; }
        internal SPOTenantSiteUrlsWithAccessParameters SitesAccParam { get; set; }
        internal SPOListsParameters ListsParam { get; set; }
        public SPOTenantListsParameters TListsParam
        {
            get { return new(SitesAccParam, ListsParam); }
        }
        public ListReportParameters(
            bool includeStorageMetrics,
            SPOTenantSiteUrlsWithAccessParameters sitesParam,
            SPOListsParameters listsParam)
        {
            IncludeStorageMetrics = includeStorageMetrics;
            SitesAccParam = sitesParam;
            ListsParam = listsParam;
        }
    }
}
