using AppsTester.Controller.Moodle;
using AppsTester.Controller.Submissions;
using AppsTester.Shared.SubmissionChecker.Events;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace AppsTester.Controller.Tests.Unit.Submissions
{
    [UsesVerify]
    public class SubmissionCheckResultsProcessorTests
    {
        [Fact]
        public async Task CorrectSubmission_HandleResultEvent_SendsToMoodleAsync()
        {
            CreateEmptyMocks(out var unitOfWorkMock, out var submissionsRepoMock, out var moodleCommuncatorMock);

            var submissionId = Guid.NewGuid();
            var submission = new SubmissionCheck
            {
                Id = submissionId,
                AttemptId = 14
            };
            submissionsRepoMock.Setup(s => s.FindSubmissionAsync(submissionId, CancellationToken.None)).Returns(Task.FromResult(submission));
            unitOfWorkMock.SetupGet(u => u.Submissions).Returns(submissionsRepoMock.Object);

            object moodleRequestParams = null;

            moodleCommuncatorMock.Setup(c => c.CallFunctionAsync(
                It.IsNotNull<string>(), It.IsNotNull<IDictionary<string, object>>(), It.IsNotNull<IDictionary<string, string>>(), CancellationToken.None))
                .Callback((string functionName, IDictionary<string, object> functionParams, IDictionary<string, string> requestParams, CancellationToken cancellationToken) =>
                {
                    moodleRequestParams = new
                    {
                        functionName,
                        functionParams,
                        requestParams
                    };
                });


            var processor = new SubmissionCheckResultsProcessor(unitOfWorkMock.Object, moodleCommuncatorMock.Object);
            await processor.HandleResultEvent(new SubmissionCheckResultEvent
            {
                SubmissionId = submissionId,
                SerializedResult = "some serialized result"
            }, CancellationToken.None);

            await Verifier.Verify(moodleRequestParams);
        }

        [Fact]
        public async Task IncorrectSubmission_HandleResultEvent_Throws()
        {
            CreateEmptyMocks(out var unitOfWorkMock, out var submissionsRepoMock, out var moodleCommuncatorMock);
            var submissionId = Guid.Parse("9A6A8525-CC55-45AA-A534-1555DE362933");

            submissionsRepoMock.Setup(s => s.FindSubmissionAsync(submissionId, CancellationToken.None)).Returns(Task.FromResult<SubmissionCheck>(null));

            var processor = new SubmissionCheckResultsProcessor(unitOfWorkMock.Object, moodleCommuncatorMock.Object);

            await Verifier.ThrowsTask(() => processor.HandleResultEvent(new SubmissionCheckResultEvent
            {
                SubmissionId = submissionId,
                SerializedResult = "some serialized result"
            }, CancellationToken.None));
        }

        [Fact]
        public async Task CorrectSubmission_HandleResultEvent_UpdatedInRepo()
        {
            CreateEmptyMocks(out var unitOfWorkMock, out var submissionsRepoMock, out var moodleCommuncatorMock);
            var submissionId = Guid.Parse("9A6A8525-CC55-45AA-A534-1555DE362933");

            var submission = new SubmissionCheck
            {
                Id = submissionId,
                AttemptId = 14
            };

            submissionsRepoMock.Setup(s => s.FindSubmissionAsync(submissionId, CancellationToken.None)).Returns(Task.FromResult(submission));

            var processor = new SubmissionCheckResultsProcessor(unitOfWorkMock.Object, moodleCommuncatorMock.Object);

            await processor.HandleResultEvent(new SubmissionCheckResultEvent
            {
                SubmissionId = submissionId,
                SerializedResult = "some serialized result"
            }, CancellationToken.None);

            unitOfWorkMock.Verify(u => u.CompleteAsync(CancellationToken.None), Times.Once);
            await Verifier.Verify(submission);
        }

        private static void CreateEmptyMocks(out Mock<IUnitOfWork> unitOfWorkMock, out Mock<ISubmissionsRepository> submissionsRepoMock, out Mock<IMoodleCommunicator> moodleCommunicatorMock)
        {
            var unitOfWorkMockLocal= new Mock<IUnitOfWork>();
            var submissionsRepoMockLocal = new Mock<ISubmissionsRepository>();
            unitOfWorkMockLocal.SetupGet(u => u.Submissions).Returns(submissionsRepoMockLocal.Object);
            var moodleCommunicatorMockLocal = new Mock<IMoodleCommunicator>();

            unitOfWorkMock = unitOfWorkMockLocal;
            submissionsRepoMock = submissionsRepoMockLocal;
            moodleCommunicatorMock = moodleCommunicatorMockLocal;
        }
    }
}
