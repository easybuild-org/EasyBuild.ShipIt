module EasyBuild.ShipIt.Tests.InitGithub

open TUnit.Core
open Tests.Utils
open Thoth.Json.Newtonsoft
open EasyBuild.ShipIt.Commands.InitGithub

let private recommendedRepository =
    {
        OwnerType = "Organization"
        AllowMergeCommit = false
        AllowSquashMerge = true
        AllowRebaseMerge = true
        SquashMergeCommitTitle = SQUASH_MERGE_COMMIT_TITLE
    }

let private changedLabels (checks: SettingCheck list) =
    checks |> List.filter (_.IsUpToDate >> not) |> List.map _.Label

[<Category("InitGithub")>]
type InitGithubTests() =

    [<Test>]
    member _.``RepositorySettings decoder reads the fields we care about``() =
        let json =
            """{
    "name": "EasyBuild.ShipIt",
    "owner": { "login": "easybuild-org", "type": "Organization" },
    "allow_merge_commit": true,
    "allow_squash_merge": true,
    "allow_rebase_merge": false,
    "squash_merge_commit_title": "COMMIT_OR_PR_TITLE",
    "squash_merge_commit_message": "COMMIT_MESSAGES"
}"""

        let actual = Decode.fromString RepositorySettings.Decoder json |> _.UnsafeOkValue

        Expect.equal
            actual
            {
                OwnerType = "Organization"
                AllowMergeCommit = true
                AllowSquashMerge = true
                AllowRebaseMerge = false
                SquashMergeCommitTitle = "COMMIT_OR_PR_TITLE"
            }

        Expect.isTrue actual.IsOwnedByOrganization

    [<Test>]
    member _.``WorkflowPermissions decoder reads the fields we care about``() =
        let json =
            """{ "default_workflow_permissions": "read", "can_approve_pull_request_reviews": false }"""

        Decode.fromString WorkflowPermissions.Decoder json
        |> _.UnsafeOkValue
        |> fun actual ->
            Expect.equal
                actual
                {
                    DefaultWorkflowPermissions = "read"
                    CanApprovePullRequestReviews = false
                }

    [<Test>]
    member _.``mergeStrategy reports nothing to change on a recommended repository``() =
        SettingCheck.mergeStrategy recommendedRepository
        |> changedLabels
        |> Expect.isEmpty

    [<Test>]
    member _.``mergeStrategy reports the settings that differ from the recommendation``() =
        let actual =
            SettingCheck.mergeStrategy
                { recommendedRepository with
                    AllowMergeCommit = true
                    AllowRebaseMerge = false
                    SquashMergeCommitTitle = "COMMIT_OR_PR_TITLE"
                }
            |> changedLabels

        Expect.equal
            actual
            [
                "Allow merge commits"
                "Allow rebase merging"
                "Default squash merge commit title"
            ]

    [<Test>]
    member _.``actionsPullRequestPermission reports nothing to change when already allowed``() =
        SettingCheck.actionsPullRequestPermission
            {
                DefaultWorkflowPermissions = "read"
                CanApprovePullRequestReviews = true
            }
        |> changedLabels
        |> Expect.isEmpty

    [<Test>]
    member _.``actionsPullRequestPermission keeps the current default workflow permissions``() =
        let actual =
            SettingCheck.actionsPullRequestPermission
                {
                    DefaultWorkflowPermissions = "write"
                    CanApprovePullRequestReviews = false
                }

        Expect.equal (changedLabels actual) [ ACTIONS_PULL_REQUEST_LABEL ]

        Expect.equal
            (actual |> List.map _.Field)
            [ Gh.Api.Field.Raw("default_workflow_permissions", "write") ]
