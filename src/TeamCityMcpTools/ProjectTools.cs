namespace TeamCityMcpTools;

public partial class ProjectTools
{
    private readonly IServiceProvider _serviceProvider;

    public ProjectTools(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
}
