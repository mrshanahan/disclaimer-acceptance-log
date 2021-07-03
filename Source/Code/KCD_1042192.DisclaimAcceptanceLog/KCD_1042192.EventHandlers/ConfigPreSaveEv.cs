using KCD_1042192.Utility;
using kCura.EventHandler;
using kCura.EventHandler.CustomAttributes;
using Relativity.API;
using Relativity.Services.Objects;
using Relativity.Services.Objects.DataContracts;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace KCD_1042192.EventHandlers
{
    [Description("Pre Save EventHandler")]
    [Guid("56ff504e-190f-4ebb-a96d-0151d94fa062")]
    public class ConfigPreSaveEv : PreSaveEventHandler, IDataEnabled
    {
        private const string ErrorMaxInstances = "Save aborted, only one Disclaimer Config object is allowed";

        public override Response Execute()
        {
            var retVal = new Response { Success = true, Message = String.Empty };
            var enabled = (bool?)ActiveArtifact.Fields["Enabled"].Value.Value;
            var allowAccessOnError = (bool?)ActiveArtifact.Fields["Allow Access On Error"].Value.Value;
            var eddsDbContext = Helper.GetDBContext(-1);

            try
            {
                //Only allow a single Config RDO to be created
                if (FirstConfigObject(retVal))
                {
                    ToggleSolution(eddsDbContext, enabled, allowAccessOnError);
                }
                else
                {
                    retVal.Message = ErrorMaxInstances;
                }
            }
            catch (Exception ex)
            {
                //Change the response Success property to false to let the user know an error occurred
                retVal.Success = false;
                retVal.Message = ex.ToString();
            }

            return retVal;
        }

        //Throw exception if this is not the first config object
        private Boolean FirstConfigObject(Response response)
        {
            var retValue = true;
            using (var proxy = Helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.System))
            {
                var workspaceId = Helper.GetActiveCaseID();
                var request = new QueryRequest
                {
                    ObjectType = new ObjectTypeRef { Guid = Utility.Constants.Guids.Objects.DisclaimerSolutionConfiguration },
                    Fields = Array.Empty<FieldRef>()
                };

                QueryResult results;
                try
                {
                    results = Task.Run(() => proxy.QueryAsync(workspaceId, request, start: 0, length: 1)).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    throw new Exception("Unable to Query Config objects.", ex);
                }

                // TODO: This logic is different from the previous logic, which technically checked that if more than zero Disclaimer Configuration
                // objects existed that the ActiveArtifact is among them. This would mean that it's okay to have multiple Disclaimer Configuration
                // objects, but you can't create new ones. Since OM pages results and since the code & comments seem to imply that the Disclaimer Configuration
                // object is a singleton, I thought that this method might be a little cleaner. If we need to stick to the old logic we can
                // leave that in.
                var moreThanOneInstanceExists = results.TotalCount > 1;
                var differentSingletonAlreadyExists = results.TotalCount == 1 && results.Objects.First().ArtifactID != ActiveArtifact.ArtifactID;
                if (moreThanOneInstanceExists || differentSingletonAlreadyExists)
                {
                    response.Success = false;
                    response.Message = ErrorMaxInstances;
                    retValue = false;
                }
                return retValue;
            }
        }

        private void ToggleSolution(IDBContext eddsDbContext, bool? enabled, bool? allowAccessOnError)
        {
            var currentRelativityVersion = Functions.GetRelativityVersion(typeof(kCura.EventHandler.Application)).ToString();
            var relativityVersion = new Version(currentRelativityVersion);
            var supportedRelativityVersion = new Version(KCD_1042192.Utility.Constants.OtherConstants.RelativityVersion.September94Release);
            if (enabled == true)
            {
                var allowAccessInsert = allowAccessOnError.GetValueOrDefault(false).ToString().ToLower();

                //THe html has many curly braces that confuse format, so String.replace was used
                var loginPageHtml = HTML.LoginPage.Replace("{0}", Utility.Constants.Guids.Applications.DisclaimerAcceptanceLog.ToString());
                loginPageHtml = loginPageHtml.Replace("{1}", allowAccessInsert);

                var parameters = new List<SqlParameter>{
                    new SqlParameter { ParameterName = "@HTML", SqlDbType = SqlDbType.NVarChar, Value = loginPageHtml }
                };

                //var currentRelativityVersion = Functions.GetAssemblyVersion(typeof(kCura.EventHandler.Application)).ToString();

                if (relativityVersion >= supportedRelativityVersion)
                {
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.EnableDisclaimerSolutionSept, parameters);
                }
                else
                {
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.EnableDisclaimerSolution, parameters);
                }
                //eddsDbContext.ExecuteNonQuerySQLStatement(SQL.EnableDisclaimerSolution, parameters);
            }
            else
            {
                if (relativityVersion >= supportedRelativityVersion)
                {
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.DisableDisclaimerSolutionSept);
                }
                else
                {
                    eddsDbContext.ExecuteNonQuerySQLStatement(SQL.DisableDisclaimerSolution);
                }
                //eddsDbContext.ExecuteNonQuerySQLStatement(SQL.DisableDisclaimerSolution);
            }
        }

        public override FieldCollection RequiredFields
        {
            get
            {
                var retVal = new FieldCollection{
                       new kCura.EventHandler.Field(Utility.Constants.Guids.Fields.ConfigurationEnabled),
                       new kCura.EventHandler.Field(Utility.Constants.Guids.Fields.ConfigurationAllowAccessOnError)
                };
                return retVal;
            }
        }
    }
}