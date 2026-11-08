using System;
using System.Runtime.InteropServices;

namespace LovingCoolMini
{
    public static class AutoStart
    {
        private const string TaskName = "LovingCoolMini";
        private const int TASK_CREATE_OR_UPDATE = 6;
        private const int TASK_LOGON_INTERACTIVE_TOKEN = 3;
        private const int TASK_RUNLEVEL_HIGHEST = 1;
        private const int TASK_TRIGGER_LOGON = 9;
        private const int TASK_ACTION_EXEC = 0;

        public static bool IsEnabled(Logger logger)
        {
            object service = null;
            object root = null;
            object task = null;
            try
            {
                service = CreateService();
                root = ((dynamic)service).GetFolder("\\");
                task = ((dynamic)root).GetTask(TaskName);
                return Convert.ToBoolean(((dynamic)task).Enabled);
            }
            catch
            {
                return false;
            }
            finally
            {
                Release(task);
                Release(root);
                Release(service);
            }
        }

        public static bool Enable(string exePath, Logger logger, out string error)
        {
            error = string.Empty;
            object service = null;
            object root = null;
            object task = null;
            try
            {
                string user = Environment.UserDomainName + "\\" + Environment.UserName;
                service = CreateService();
                root = ((dynamic)service).GetFolder("\\");
                task = ((dynamic)service).NewTask(0);

                dynamic definition = task;
                definition.RegistrationInfo.Description = "LOVINGCOOL Mini CPU 温度数显后台程序";
                definition.Settings.Enabled = true;
                definition.Settings.StartWhenAvailable = true;
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.StopIfGoingOnBatteries = false;
                definition.Settings.ExecutionTimeLimit = "PT0S";
                definition.Settings.MultipleInstances = 2;
                definition.Principal.UserId = user;
                definition.Principal.LogonType = TASK_LOGON_INTERACTIVE_TOKEN;
                definition.Principal.RunLevel = TASK_RUNLEVEL_HIGHEST;

                dynamic trigger = definition.Triggers.Create(TASK_TRIGGER_LOGON);
                trigger.UserId = user;

                dynamic action = definition.Actions.Create(TASK_ACTION_EXEC);
                action.Path = exePath;
                action.Arguments = "--minimized";

                ((dynamic)root).RegisterTaskDefinition(TaskName, task, TASK_CREATE_OR_UPDATE,
                    null, null, TASK_LOGON_INTERACTIVE_TOKEN);
                bool verified = IsEnabled(logger);
                logger.Info("已创建开机自启计划任务: " + exePath + "，验证=" + verified);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                logger.Error("创建开机自启任务失败", ex);
                return false;
            }
            finally
            {
                Release(task);
                Release(root);
                Release(service);
            }
        }

        public static bool Disable(Logger logger, out string error)
        {
            error = string.Empty;
            object service = null;
            object root = null;
            try
            {
                service = CreateService();
                root = ((dynamic)service).GetFolder("\\");
                ((dynamic)root).DeleteTask(TaskName, 0);
                logger.Info("已删除开机自启计划任务");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                logger.Error("删除开机自启任务失败", ex);
                return false;
            }
            finally
            {
                Release(root);
                Release(service);
            }
        }

        private static object CreateService()
        {
            Type type = Type.GetTypeFromProgID("Schedule.Service");
            if (type == null)
            {
                throw new InvalidOperationException("找不到任务计划程序服务");
            }
            object service = Activator.CreateInstance(type);
            ((dynamic)service).Connect();
            return service;
        }

        private static void Release(object obj)
        {
            try
            {
                if (obj != null && Marshal.IsComObject(obj))
                {
                    Marshal.FinalReleaseComObject(obj);
                }
            }
            catch
            {
            }
        }
    }
}
