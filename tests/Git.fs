module EasyBuild.ShipIt.Tests.Git

open Thoth.Yaml
open TUnit.Core
open Tests.Utils

// We can't test all the Git functions, because:
// - getHeadBranchName: we can't garantee the branch name
// - isDirty: we can't garantee the repository is clean
// - getCommits(All): number of commits change over time
// - getCommits(From): number of commits change over time
//
// But we still test some of the function to try minimize the risk of bugs and regressions

[<Category("Git")>]
type GitTests() =

    [<Test>]
    member _.``readCommits works``() =
        let actual = Git.readCommit "453db51d8c72c977cc4dcf5decf4cf03d6b1f28c"

        let expected: Git.Commit =
            {
                Hash = "453db51d8c72c977cc4dcf5decf4cf03d6b1f28c"
                AbbrevHash = "453db51"
                Author = "Mangel Maxime"
                ShortMessage = "chore: update .NET tools"
                RawBody =
                    "chore: update .NET tools
"
                Files =
                    [
                        ".config/dotnet-tools.json"
                        "src/Generate/ReleaseContext.fs"
                        "src/Generate/Updater.fs"
                        "src/Tools/Git.fs"
                        "tests/Thoth.Yaml.Decode.fs"
                        "tests/fixtures/files/update_version.fsx"
                    ]
            }

        Expect.equal actual expected

    [<Test>]
    member _.``readCommit works for commits with quotes in the message``() =
        let actual = Git.readCommit "0cfd2cbead888deff33a7ee839416992ebe9fecb"

        let expected: Git.Commit =
            {
                Hash = "0cfd2cbead888deff33a7ee839416992ebe9fecb"
                AbbrevHash = "0cfd2cb"
                Author = "Maxime Mangel"
                ShortMessage =
                    "feat: add `--skip-merge-commit` allowing to skip commit starting with \"Merge \""
                RawBody =
                    """feat: add `--skip-merge-commit` allowing to skip commit starting with "Merge "
"""
                Files =
                    [
                        "README.md"
                        "src/Generate/ReleaseContext.fs"
                        "src/Generate/Types.fs"
                        "tests/ReleaseContext.fs"
                        "tests/Utils.fs"
                    ]
            }

        Expect.equal actual expected

    [<Test>]
    member _.``tryGetRemoteFromUrl works with https``() =
        let actual = Git.tryGetRemoteFromUrl "https://github.com/owner/repo.git"

        let expected: Git.Remote =
            {
                Hostname = "github.com"
                Owner = "owner"
                Repository = "repo"
            }

        Expect.equal actual (Some expected)

    [<Test>]
    member _.``tryGetRemoteFromUrl works even without .git suffix``() =
        let actual = Git.tryGetRemoteFromUrl "https://github.com/owner/repo"

        let expected: Git.Remote =
            {
                Hostname = "github.com"
                Owner = "owner"
                Repository = "repo"
            }

        Expect.equal actual (Some expected)

    [<Test>]
    member _.``tryGetRemoteFromUrl returns None when url is invalid``() =
        let actual = Git.tryGetRemoteFromUrl "https://github.com/missing-segments"

        Expect.isNone actual

    [<Test>]
    member _.``tryGetRemoteFromSSH works with ssh``() =
        let actual = Git.tryGetRemoteFromSSH "git@github.com:owner/repo.git"

        let expected: Git.Remote =
            {
                Hostname = "github.com"
                Owner = "owner"
                Repository = "repo"
            }

        Expect.equal actual (Some expected)

    [<Test>]
    member _.``tryGetRemoteFromSSH works even without .git suffix``() =
        let actual = Git.tryGetRemoteFromSSH "git@github.com:owner/repo"

        let expected: Git.Remote =
            {
                Hostname = "github.com"
                Owner = "owner"
                Repository = "repo"
            }

        Expect.equal actual (Some expected)

    [<Test>]
    member _.``tryGetRemoteFromSSH returns None when url is invalid``() =
        Expect.equal (Git.tryGetRemoteFromSSH "github.com:owner/repo.git") None
        Expect.equal (Git.tryGetRemoteFromSSH "git@github.comowner/repo.git") None
        Expect.equal (Git.tryGetRemoteFromSSH "git@github.com:ownerrepo.git") None
