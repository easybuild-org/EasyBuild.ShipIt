# EasyBuild.ShipIt

[![NuGet](https://img.shields.io/nuget/v/EasyBuild.ShipIt.svg)](https://www.nuget.org/packages/EasyBuild.ShipIt)

[![Sponsors badge link](https://img.shields.io/badge/Sponsors_this_project-EA4AAA?style=for-the-badge)](https://mangelmaxime.github.io/sponsors/)

Tool for generating changelog based on Git history based on [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/). It is using [EasyBuild.CommitParser](https://github.com/easybuild-org/EasyBuild.CommitParser) to parse commit messages check their documentation for more information about configuration.

## Features

- Easy integration into your CI/CD pipeline 🛠
- Automatic versioning based on commit messages 🚀
- Can create/update pull request for automatic releases 🔧
- Support for monorepo 🔥

## How does it work?

EasyBuild.ShipIt search for any `CHANGELOG.md`, for each of them look at the commits since the last
released commit (based on the `last_commit_released` configuration) and generate a new changelog entry based on the commit messages.

Optionally, it will create a pull request or commit the changes directly to the current branch based on the [`mode`](#--mode) configuration.

Learn more about:

- [How is the version calculated?](#how-is-the-version-calculated)
- [Commit conventions](#commit-conventions)
- [CLI options](#cli-options)
- [Configuration](#configuration)
- [Monorepo support](#monorepo-support)

## Usage

```bash
# Install the tool
dotnet tool install EasyBuild.ShipIt

# Run the tool
dotnet shipit
```

### CLI manual

```text
DESCRIPTION:
Automate changelog generation based on conventional commit messages and create pull requests for releases.

The tool will do its best to automatically detect the Git provider based on the git remote URL.
You can force it to use a specific provider using sub-commands, e.g. 'shipit github' to force using GitHub.

Learn more at https://github.com/easybuild-org/EasyBuild.ShipIt

USAGE:
    shipit [OPTIONS] [COMMAND]

OPTIONS:
                                        DEFAULT
    -h, --help                                          Prints help information
        --mode <MODE>                   pull-request    Mode of operation. Possible values are 'local', 'pull-request' and 'push'
        --pre-release [PREFIX]          beta            Indicate that the generated version is a pre-release version. Optionally, you can provide a prefix for the beta version. Default is 'beta'
        --remote-hostname <HOSTNAME>                    Git remote hostname, e.g. github.com, gitlab.com
        --remote-owner <OWNER>                          Git remote owner or organization name
        --remote-repo <REPO>                            Git remote repository name
        --skip-invalid-commit           False           Skip invalid commits instead of failing
        --skip-merge-commit             False           Skip merge commits when generating the changelog (commit messages starting with 'Merge ')
    -v, --version                                       Show version information

COMMANDS:
    version
    conventions    List supported Conventional Commit types
    github         Publish to GitHub
```

## How is the version calculated?

### Stable versions

The version is calculated based on the commit messages since last released, who are contributing to the changelog file (based on the `include` and `exclude` configuration).

Rules are the following:

- A `breaking change` commit will bump the major version

    ```text
    * chore: release 1.2.10
    * feat!: first feature # => 2.0.0
    ```

- `feat` commits will bump the minor version

    ```text
    * chore: release 1.2.10
    * feat: first feature
    * feat: second feature # => 1.3.0
    ```

- `perf` commits will bump the minor version

    ```text
    * chore: release 1.2.10
    * perf: first performance improvement
    * perf: second performance improvement # => 1.3.0
    ```

- `fix` commits will bump the patch version

    ```text
    * chore: release 1.2.10
    * fix: first fix
    * fix: second fix # => 1.2.11
    ```

You can mix different types of commits, the highest version will be used (`breaking change` > `feat` or `perf` > `fix`).

```text
* chore: release 1.2.10
* feat: first feature
* perf: first performance improvement
* fix: first fix # => 1.3.0
```

### Pre-release versions

A pre-release will be generated if you set [`pre_release`](#pre_release) configuration or if you pass `--pre-release` CLI option.

Rules are the following:

- If the previous version is **stable**, then we compute the standard version bump and start a new pre-release version.

    ```text
    * chore: release 1.2.10
    * feat: first feature
    * fix: first fix # => 1.3.0-beta.1
    ```

- If the previous version is a **pre-release**, with the same suffix, then we increment the pre-release version.

    ```text
    * chore: release 1.3.0-beta.10
    * feat: first feature
    * fix: first fix # => 1.3.0-beta.11
    ```

- If the previous version is a **pre-release**, with a different suffix, then we use the same base version and start a new pre-release version.

    ```text
    * chore: release 1.3.0-alpha.10
    * feat: first feature
    * fix: first fix # => 1.3.0-beta.1
    ```

**💡 Tips**

EasyBuild.Changelog use the last version in the changelog file to compute the next version.

For this reason, while working on a pre-release, it is advised to work in a separate branch from the main branch. This allows you to work on the pre-release while still being able to release new versions on the main branch.

```text
* chore: release 1.2.10
| \
|  * feat!: remove `foo` API
|  * feat: add `bar` API        # => 2.0.0.beta.1
|  * fix: fix `baz` API
* fix: fix `qux` API
* chore: release 1.2.11
|  * fix: fix `qux` API         # => 2.0.0.beta.2
| /
* chore: release 2.0.0          # => 2.0.0
```

### Moving out of pre-release

If you want to move out of pre-release, you need to remove the [`pre_release`](#pre_release) configuration or stop passing the `--pre-release` CLI option.

Then the next version will be released using the base version of the previous pre-release.

```text
* chore: release 1.3.0-beta.10
* feat: first feature
* fix: first fix # => 1.3.0
```

> [!TIP]
> If you are not sure what will be calculated, you can use the `--skip-pull-request` option to see the result without creating a pull request.
>
> You can then reset the changelog using `git restore path/to/CHANGELOG.md` before re-running the command.

### Overriding the computed version

If the computed version is not what you want, you can use [`force_version`](#force_version) configuration to override the computed version for a specific release.

## Commit conventions

EasyBuild.Shipit follows the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) specification.

```text
<type>[optional scope][optional !]: <description>

[optional body]

[optional footer]
```

- `[optional body]` is a free-form text.

    ```text
    This is a single line body.
    ```

    ```text
    This is a

    multi-line body.
    ```

- `[optional footer]` is inspired by [git trailer format](https://git-scm.com/docs/git-interpret-trailers) `key: value` but also allows `key #value`

    ```text
    BREAKING CHANGE: <description>
    Signed-off-by: Alice <alice@example.com>
    Signed-off-by: Bob <bob@example.com>
    Refs #123
    Tag: cli
    ```

The following commit types are supported:

- **feat** : A new feature
- **fix** : A bug fix
- **ci** : Changes to CI/CD configuration
- **chore** : Changes to the build process or auxiliary tools and libraries such as documentation generation
- **docs** : Documentation changes
- **test** : Adding missing tests or correcting existing tests
- **style** : Changes that do not affect the meaning of the code (white-space, formatting, missing semi-colons, etc)
- **refactor** : A code change that neither fixes a bug nor adds a feature
- **perf** : A code change that improves performance
- **revert** : Reverts a previous commit
- **build** : Changes that affect the build system or external dependencies

## CLI options

### `--remote-hostname`, `--remote-owner`, `--remote-repo`

These options allow you to specify the Git remote information. This is useful when the tool is not able to automatically detect the Git provider based on the git remote URL.

This information is used to create links to commits, diff, etc. in the generated changelog file.

### `--skip-invalid-commit`

**type:** bool
**default:** false

When this option is passed, the tool will skip any commit that is not following the Conventional Commits format instead of failing.

### `--skip-merge-commit`

**type:** bool
**default:** false

When this option is passed, the tool will skip any merge commit (commit messages starting with 'Merge ') when generating the changelog.

### `--pre-release`

**type:** string

When this option is passed, the generated version will be a pre-release version.

Optionsally, you can provide a prefix for the pre-release version. If no prefix is provided, it will default to `beta`.

### `--mode`

**type:** string
**default:** `pull-request`

Control the mode in which the tool operates.

- `local`: Only generate the changelog file locally
- `pull-request`: Create a pull request with the updated changelog file (default)
- `push`: Push the updated changelog file directly to the current branch

## Configuration

EasyBuild.ShipIt configuration lives as a front matter in your `CHANGELOG.md` file(s).

```text
---
last_commit_released: abcd1234
include:
  - ../Shared/
---

# Changelog

All notable changes to this project will be documented in this file.

...
```

### `last_commit_released`

**type:** string

This is the commit hash of the last released commit. It is used to determine which commits should be considered for the next release.

You should set it up manually only if you are adopting EasyBuild.ShipIt in an existing project. If you are starting a new project, you can leave it empty and it will be automatically set to the latest commit hash when you run the tool for the first time.

### `include`

**type:** string[]

Allows to include commits from other paths. This is useful for monorepo where you have multiple projects in the same repository.

```yml
include:
  - ../Shared/
  - ../Lib/
```

> [!NOTE]
> It always include files in the same directory as the changelog file, so you don't need to include it in the configuration.

### `exclude`

**type:** string[]

Allows to exclude commits from specific paths.

```yml
exclude:
  - tests/
```

### `pre_release`

**type:** string

When set to a non-empty value, the generated version will be a pre-release version with the provided value as prefix.

```yml
pre_release: beta
```

### `priority`

**type:** int

Lowest number has the highest priority. This is useful to determine which changelog file should be updated first when generating a new release.

If a changelog has no priority, it will be considered as having the lowest priority (i.e. it will be updated last).

```yml
priority: 1
```

### `name`

**type:** string

Allows to override the name of the project used when reporting in PR.

By default, the project is named after the parent directory of the changelog file.

### `force_version`

**type:** string

Allows to force the version to be used in the changelog. This is useful when you want to override the calculated version for a specific release.

> [!IMPORTANT]
> This is not persisted in the generated changelog file, it is only used for the current run.

```yml
force_version: 2.0.0
```

### `updaters`

**type:** Updater[]

List of updaters to run after generating the changelog. This allows you to automatically update other files in your repository, e.g. `package.json` or `AssemblyInfo.cs`, with the new version.

#### `regex`

**type:** object

Use a regex pattern to find the text to replace with the new version.

| Property | Description |
| --- | --- |
| `file` | Relative path to the file to update. It is relative to the changelog file. |
| `pattern` | Pattern used to find the text to replace. |

> [!IMPORTANT]
> The regex with replace the full match with the new version. Make sure to use a regex that only matches the version part of the file.
>
> For example, if you want to update:
>
> ```xml
> <Metadata>
>   <Version>1.0.0</Version>
> </Metadata>
> ```
>
> Your regex should be `(?<=<Version>).*(?=</Version>)` to only replace the version part and not the full match.

```yml
updaters:
  - regex:
      file: Metadata.xml
      pattern: (?<=<Version>).*(?=</Version>)
```

#### `package.json`

**type:** object

Update the `version` field in a `package.json` file.

| Property | Description |
| --- | --- |
| `file` | Relative path to the `package.json` file to update. It is relative to the changelog file. |

```yml
updaters:
  - package.json:
      file: path/to/package.json
```

#### `json`

**type:** object

Update a JSON file using a JSON Patch. It uses the [JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902) format to specify the changes to be made to the JSON file.

| Property | Description |
| --- | --- |
| `file` | Relative path to the JSON file to update. It is relative to the changelog file. |
| `pointer` | JSON pointer to the field to update. |

```yml
updaters:
  - json:
      file: path/to/file.json
      pointer: /metadata/version
```

#### `xml`

**type:** object

Update an XML file using an XPath expression to find the node to update.

| Property | Description |
| --- | --- |
| `file` | Relative path to the XML file to update. It is relative to the changelog file. |
| `selector` | XPath expression to find the node to update. |

```yml
updaters:
  - xml:
      file: path/to/file.xml
      selector: /Metadata/Version
```

#### `command`

**type:** string

Run an arbitrary command to update the version. The command will receive the new version as an argument in place of `{version}`.

```yml
updaters:
  - command: "./update-version.sh {version}"
```

## Monorepo support

EasyBuild.ShipIt supports monorepo.

For example, if you have the following repository structure:

```text
repo/
├── project-a/
│   ├── CHANGELOG.md
│   └── ...
├── project-b/
│   ├── CHANGELOG.md
│   └── ...
└── Shared/
    └── ...
```

It means that 2 projects will be released, `project-a` and `project-b`. By default, only the commits that are in the same directory as the changelog file will be considered for the release of each project.

If you want to include commits from the `Shared` directory for both projects, you can use the `include` configuration to include the `Shared` directory for both changelog files.

Learn more about the `include` configuration in the [Configuration](#configuration) section.

## Recipes

### Prod / staging environments

When working with production and staging environments, you can use the `pre_release` configuration to generate pre-release versions for the staging environment and stable versions for the production environment.

To do that, you need use `--pre-release` CLI option in your staging environment and not use it in your production environment.

We want to use the CLI option here instead of the configuration as it allows to easily switch between pre-release and stable versions without having to change the configuration file.

## Exit codes

- `0`: Success
- `1`: Error
- `100`: Help was requested
