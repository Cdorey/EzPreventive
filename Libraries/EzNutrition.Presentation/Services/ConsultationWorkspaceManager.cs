using AntDesign;
using EzNutrition.Application.Archives;
using EzNutrition.Application.Consultations;
using EzNutrition.Application.Ports;
using EzNutrition.Archives.Contracts.Identity;
using EzNutrition.Archives.Contracts.Serialization;
using EzNutrition.Domain.Consultations;
using Microsoft.AspNetCore.Components;
using System.Collections.Concurrent;

namespace EzNutrition.Presentation.Services
{
    public class ConsultationWorkspaceManager(IMessageService message,
                                      UserSessionService userSession,
                                      NavigationManager navigationManager,
                                      ILogger<ConsultationWorkspaceManager> logger,
                                      ArchiveContractAssembler contractAssembler,
                                      ConsultationApplicationService consultationService) : ConcurrentDictionary<Guid, ConsultationWorkspace>()
    {
        public event EventHandler? ClientNameChanged;

        public Guid NewWorkspace()
        {
            var client = new ClientInfo();
            return AddWorkspace(client, new ConsultationWorkspace(client));
        }

        /// <summary>
        /// 使用既有患者身份建立一次新的独立咨询，并用最近一次快照预填表单。
        /// </summary>
        public Guid NewWorkspace(ArchivePatientContext patientContext)
        {
            ArgumentNullException.ThrowIfNull(patientContext);
            var client = new ClientInfo
            {
                Name = patientContext.Name,
                Gender = patientContext.Gender,
                BirthDate = patientContext.BirthDate,
                Age = patientContext.Age,
                Height = patientContext.HeightInCentimeters,
                Weight = patientContext.WeightInKilograms,
                SpecialPhysiologicalPeriod = patientContext.PhysiologicalState ?? string.Empty
            };
            return AddWorkspace(client, new ConsultationWorkspace(client, patientContext));
        }

        /// <summary>确认会话并初始化咨询；令牌续期失败时保留已有工作区内容。</summary>
        public async Task ClientInfoConfirmed(ConsultationWorkspace archive, CancellationToken cancellationToken = default)
        {
            if (archive.IsLoading)
            {
                return;
            }

            if (string.IsNullOrEmpty(archive.Client.Gender))
            {
                await message.ErrorAsync("性别不能为空");
                return;
            }

            if (archive.Client.Age is null)
            {
                await message.ErrorAsync("年龄不符合逻辑");
                return;
            }

            var initializationStarted = false;
            try
            {
                archive.IsLoading = true;
                if (!await userSession.EnsureAuthenticatedAsync(cancellationToken))
                {
                    navigationManager.NavigateTo("/");
                    await message.ErrorAsync("需要登录");
                    return;
                }
                initializationStarted = true;
                await consultationService.InitializeAsync(archive, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (SessionAuthenticationException exception)
            {
                navigationManager.NavigateTo("/");
                await message.ErrorAsync(exception.Message);
            }
            catch (Exception ex) when (ex is NutritionDataAccessException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Unable to initialize archive for nutrition assessment.");
                await message.ErrorAsync("初始化营养评估失败，请检查网络后重试。");
                if (!initializationStarted)
                {
                    return;
                }
                archive.CurrentEnergyCalculator = null;
                archive.DRIs = null;
                archive.DietaryRecallSurvey = null;
                archive.DietaryTower = null;
                archive.NutritionAssessments.Clear();
                archive.ClientInfoFormEnabled = true;
                archive.SubjectiveObjectiveAssessmentPlanInformation = null;
            }
            finally
            {
                archive.IsLoading = false;
            }
        }

        public async Task ClientInfoConfirmed(Guid archiveId, CancellationToken cancellationToken = default)
        {
            if (!TryGetValue(archiveId, out var archive))
            {
                throw new KeyNotFoundException($"Consultation workspace {archiveId} does not exist.");
            }

            await ClientInfoConfirmed(archive, cancellationToken);
        }

        /// <summary>
        /// 建立指定运行态咨询的格式无关档案文档快照。
        /// </summary>
        /// <param name="archiveId">运行态咨询标识。</param>
        /// <param name="capturedAt">快照时间。</param>
        /// <param name="bundleId">可选资源包标识。</param>
        /// <returns>档案文档快照。</returns>
        /// <exception cref="KeyNotFoundException">运行态咨询不存在。</exception>
        public ArchiveDocument CreateContractDocument(
            Guid archiveId,
            DateTimeOffset? capturedAt = null,
            ArchiveBundleId? bundleId = null)
        {
            if (!TryGetValue(archiveId, out var archive))
            {
                throw new KeyNotFoundException($"Consultation workspace {archiveId} does not exist.");
            }

            return contractAssembler.CreateDocument(archive, capturedAt, bundleId);
        }

        private void HandleClientNameChanged(object? sender, EventArgs e) =>
            ClientNameChanged?.Invoke(sender, e);

        private Guid AddWorkspace(ClientInfo client, ConsultationWorkspace workspace)
        {
            this[client.ClientId] = workspace;
            client.NameChanged += HandleClientNameChanged;
            return client.ClientId;
        }
    }
}
