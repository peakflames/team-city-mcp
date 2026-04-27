# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Fixed

## [0.2.0] - 2026-04-27

### Added
- `teamcity_list_projects` tool to list all TeamCity projects
- `teamcity_get_project` tool to retrieve details of a specific project
- `teamcity_get_project_hierarchy` tool to navigate the full project hierarchy
- `teamcity_list_build_types` tool to list build configurations within a project
- `teamcity_get_build_type` tool to retrieve build configuration details including steps, triggers, and agent requirements
- `teamcity_get_build_status` tool to check the status of a specific build
- `teamcity_get_queued_builds` tool to list builds currently in the queue
- `teamcity_get_running_builds` tool to list builds currently running
- `teamcity_search_builds` tool to search builds with filtering options
- `teamcity_server_info` tool to retrieve TeamCity server information
- Enriched build and build-type models with steps, triggers, and agent info

## [0.1.0] - 2026-04-23

### Added
- Initial release of TeamCity MCP Servers
- Support for TeamCity CI/CD integration via MCP (Model Context Protocol)
- `list_builds` tool to get builds for a given build type/configuration
- `get_build_details` tool to retrieve detailed information about a specific build
- TeamCityRemoteMcpServer (ASP.NET Web API-based) with Streamable HTTP and SSE transport
- TeamCityMcpServer (Console-based) with stdio transport
- TeamCity HTTP client with Bearer token authentication and factory pattern
- Docker support with built-in .NET SDK container capabilities
- GitHub Actions workflow for automated Docker Hub publishing on version tag push
- Environment variable support for secure credential management
