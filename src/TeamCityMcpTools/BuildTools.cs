namespace TeamCityMcpTools;

public partial class BuildTools
{
    private readonly IServiceProvider _serviceProvider;

    public BuildTools(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
}
