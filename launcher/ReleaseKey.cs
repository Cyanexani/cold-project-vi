// edupode's release key, public half: the launcher installs an update only when manifest.json is
// signed with the private half (scripts\new-release-key.ps1 made it; it never leaves edupode's PC).
namespace VIctoryLauncher
{
    static class ReleaseKey
    {
        public const string PublicXml = "<RSAKeyValue><Modulus>yt4Zt0Sed9Cu8T4BXatkqgeiD+zSd37fJilTOGmaCi2+Gl7Dguar0nJ6Op4YiVmzIMNee13YUwq88YisibAdWdev99MzZMh3nyXb1fA4y4lVX/dpY8iSJjWIEIoDesw0tH61eU//TtFtkFkNbxgQ+/OvtfkSCjdjTBJy4O27/vb039/+TT6zlApeQm2jbu7C/ATgNWloPfihADNkUgxCQU6MtKAClQ47ozo4szS/fonVgG0nyPfLvwhBjD5zyTAzypNXuAtvhHIJ6BCrJacQy/MDDw56eFDaQK9A8Fslu+gWO7+pkjFlGLYDEPtXBBqGNx6hfFeDqD+aFgsdeXdlLNlg8OkEQITaeRdfsUBNeEF1MdZEOYMNSy8NSGNKowAhHA6SoCLaWWl1Vd+ZuBmguN9dHXJEpIwiVSuHedyTBvPUh69TCHLTaYepXBVEC0MRwESHaRyPV8BVWxQrrd5UKqEQDuXNXPVi4L4HUUOUqaFq/VFeXayAd+EC5TPL3oPp</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    }
}