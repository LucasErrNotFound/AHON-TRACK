using System.Collections.Generic;
using AHON_TRACK.ViewModels;

namespace AHON_TRACK.Services.Interface;

public interface IPackageService
{
    List<Package> GetPackages();
}