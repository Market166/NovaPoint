﻿using Microsoft.SharePoint.Client;
using NovaPointLibrary.Commands.Authentication;
using NovaPointLibrary.Solutions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace NovaPointLibrary.Commands.Site
{
    internal class GetSubsite
    {
        private LogHelper _logHelper;
        private readonly AppInfo _appInfo;
        private readonly string AccessToken;
        internal GetSubsite(LogHelper logHelper, AppInfo appInfo, string accessToken)
        {
            _logHelper = logHelper;
            _appInfo = appInfo;
            AccessToken = accessToken;
        }

        internal List<Web> CsomAllSubsitesBasicExpressions(string siteUrl)
        {
            _appInfo.IsCancelled();
            _logHelper.AddLogToTxt($"{GetType().Name}.CsomAllSubsitesBasicExpressions - Start getting all Subsites with basic expresions");

            var retrievalExpressions = new Expression<Func<Web, object>>[]
            {
                w => w.Id,
                w => w.Url,
                w => w.Title,
                w => w.ServerRelativeUrl,
                w => w.WebTemplate,
                w => w.LastItemModifiedDate,
            };

            var results = CsomAllSubsites(siteUrl, retrievalExpressions);

            _logHelper.AddLogToTxt($"{GetType().Name}.CsomAllSubsitesBasicExpressions - Finish getting all Subsites with basic expresions");
            return results;
        }

        internal List<Web> CsomAllSubsitesWithRoles(string siteUrl)
        {
            _appInfo.IsCancelled();
            _logHelper.AddLogToTxt($"{GetType().Name}.CsomAllSubsitesWithRoles - Start getting all Subsites with roles");

            var retrievalExpressions = new Expression<Func<Web, object>>[]
            {
                w => w.HasUniqueRoleAssignments,
                w => w.Id,
                w => w.RoleAssignments.Include(
                    ra => ra.RoleDefinitionBindings,
                    ra => ra.Member),
                w => w.ServerRelativeUrl,
                w => w.Title,
                w => w.Url,
            };

            var results = CsomAllSubsites(siteUrl, retrievalExpressions);

            _logHelper.AddLogToTxt($"{GetType().Name}.CsomAllSubsitesWithRoles - Finish getting all Subsites with roles");
            return results;

        }

        internal List<Web> CsomAllSubsitesWithRolesAndSiteDetails(string siteUrl)
        {
            _appInfo.IsCancelled();
            _logHelper.AddLogToTxt($"{GetType().Name}.CsomAllSubsitesWithRolesAndSiteDetails - Start getting all Subsites with roles");

            var retrievalExpressions = new Expression<Func<Web, object>>[]
            {
                w => w.HasUniqueRoleAssignments,
                w => w.Id,
                w => w.LastItemModifiedDate,
                w => w.RoleAssignments.Include(
                    ra => ra.RoleDefinitionBindings,
                    ra => ra.Member),
                w => w.ServerRelativeUrl,
                w => w.Title,
                w => w.Url,
                w => w.WebTemplate,
            };

            var results = CsomAllSubsites(siteUrl, retrievalExpressions);

            _logHelper.AddLogToTxt($"{GetType().Name}.CsomAllSubsitesWithRolesAndSiteDetails - Finish getting all Subsites with roles");
            return results;

        }

        private List<Web> CsomAllSubsites(string siteUrl, Expression<Func<Web, object>>[] retrievalExpressions)
        {
            _appInfo.IsCancelled();
            _logHelper.AddLogToTxt( $"{GetType().Name}.CsomAllSubsites - Start getting all Subsites");

            using var clientContext = new ClientContext(siteUrl);
            clientContext.ExecutingWebRequest += (sender, e) =>
            {
                e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + AccessToken;
            };

            List<Web> results = new();

            var subsites = clientContext.Web.Webs;

            clientContext.Load(subsites);
            clientContext.ExecuteQueryRetry();

            results.AddRange(GetSubWebsInternal(subsites, retrievalExpressions));

            _logHelper.AddLogToTxt($"{GetType().Name}.CsomAllSubsites - Finish getting all Subsites");
            return results;

        }

        private List<Web> GetSubWebsInternal(WebCollection subsites, Expression<Func<Web, object>>[] retrievalExpressions)
        {
            _appInfo.IsCancelled();
            _logHelper.AddLogToTxt($"{GetType().Name}.GetSubWebsInternal - Start getting Subsites internals");

            var subwebs = new List<Web>();

            // Retrieve the subsites in the provided webs collection
            subsites.EnsureProperties(new Expression<Func<WebCollection, object>>[] { wc => wc.Include(w => w.Id) });

            foreach (var subsite in subsites)
            {
                // Retrieve all the properties for this particular subsite
                subsite.EnsureProperties(retrievalExpressions);
                subwebs.Add(subsite);

                subwebs.AddRange(GetSubWebsInternal(subsite.Webs, retrievalExpressions));

            }

            _logHelper.AddLogToTxt($"{GetType().Name}.GetSubWebsInternal - Finish getting Subsites internals");
            return subwebs;
        }
    }
}