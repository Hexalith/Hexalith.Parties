from pathlib import Path
import sys
root=Path(sys.argv[1]).resolve()
workspace=Path(sys.argv[2]).resolve()
fc=root/'references/Hexalith.FrontComposer'
es=root/'references/Hexalith.EventStore'
files=[]
def add(base,*patterns):
 for pattern in patterns: files.extend(base.glob(pattern))
add(root,'src/Hexalith.Parties.UI/Services/OptimisticReconcile*.cs','src/Hexalith.Parties.UI/Services/PartiesProjection*.cs','src/Hexalith.Parties.UI/Services/ProjectionFreshnessOptions.cs','src/Hexalith.Parties.UI/Services/ProjectionFreshnessFallback.cs','src/Hexalith.Parties.UI/Services/CommandAcceptance.cs','src/Hexalith.Parties.UI/Services/IProjectionStream.cs','src/Hexalith.Parties.UI/Services/IProjectionAccessTokenProvider.cs','src/Hexalith.Parties.UI/Services/EventStoreSignalRProjectionStream.cs','src/Hexalith.Parties.UI/Status/*.cs','src/Hexalith.Parties.Client/PartiesClientException.cs','src/Hexalith.Parties.Client/AdminPortal/AdminPortalGdprCommandResult.cs','src/Hexalith.Parties.Client/AdminPortal/AdminPortalGdprOutcome.cs','src/Hexalith.Parties.Contracts/Models/ProjectionFreshness*.cs','src/Hexalith.Parties.Contracts/PartyProjectionNames.cs','tests/Hexalith.Parties.UI.Tests/OptimisticReconcileTests.cs','tests/Hexalith.Parties.UI.Tests/PartiesProjectionSubscriptionTests.cs','tests/Hexalith.Parties.UI.Tests/ProjectionFreshnessFallbackTests.cs','tests/Hexalith.Parties.UI.Tests/FakeProjectionStream.cs','tests/Hexalith.Parties.UI.Tests/ManualTimeProvider.cs')
add(fc,'src/Hexalith.FrontComposer.Contracts.UI/Freshness/FcFreshnessStatus.cs','src/Hexalith.FrontComposer.Contracts.UI/Freshness/FcReconcileState.cs','src/Hexalith.FrontComposer.Contracts.UI/Freshness/FcOptimisticReconcileRequest.cs','src/Hexalith.FrontComposer.Contracts.UI/Downloads/IFcBrowserDownloadService.cs','src/Hexalith.FrontComposer.Shell/Services/Feedback/FcOptimisticReconciler.cs','src/Hexalith.FrontComposer.Shell/Services/Downloads/FcBrowserDownloadService.cs','src/Hexalith.FrontComposer.Shell/Extensions/FrontComposerFeedbackServiceExtensions.cs','tests/Hexalith.FrontComposer.Shell.Tests/Services/FcOptimisticReconcilerTests.cs','tests/Hexalith.FrontComposer.Shell.Tests/Services/DelayedReconcileSubscription.cs')
add(es,'src/Hexalith.EventStore.SignalR/*.cs','tests/Hexalith.EventStore.SignalR.Tests/EventStoreSignalRClientTests.cs','tests/Hexalith.EventStore.SignalR.Tests/StalledProjection*.cs')
for mode in ['retained','shared','shared-legacy-transport','retained-cancellable-transport']:
 directory=workspace/mode;directory.mkdir(parents=True,exist_ok=True)
 constants='HFC_SHARED_UI'
 if mode in ['shared','shared-legacy-transport']:
  constants+=';HFC_SHARED_RECONCILE_READINESS'
 if mode in ['shared','retained-cancellable-transport']:
  constants+=';HES_CANCELLABLE_PROJECTIONS'
 project=f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><DefineConstants>{constants}</DefineConstants></PropertyGroup>
<ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /><Using Include="Xunit" />
'''
 for name,version in [('xunit.v3','4.0.2'),('Shouldly','4.3.0'),('NSubstitute','6.2.0'),('Microsoft.AspNetCore.SignalR.Client','10.0.14'),('Microsoft.Extensions.TimeProvider.Testing','10.11.0')]:
  project+=f'<PackageReference Include="{name}" Version="{version}" />\n'
 for f in sorted(set(files)): project+=f'<Compile Include="{f}" Link="{f.relative_to(root)}" />\n'
 project+='</ItemGroup></Project>\n'
 (directory/'Focused.csproj').write_text(project)
 print(mode,len(set(files)))
