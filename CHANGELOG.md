# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Fixed

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
