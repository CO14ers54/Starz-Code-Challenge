using System;
using System.Collections.Generic;
using System.Text;
//using Microsoft.Extensions.Logging;
using System.Configuration;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Data.SqlClient;
using System.Data;

namespace FruitCalculator
{
   class Logger
   {
      #region Members
      private static Logger m_instance = null;
      private static object m_syncLock = new object();
      private EventLog m_eventLog = null;
      private enum LogType { Console, Database, EventViewer, All };
      private int m_eventId = 1;
      private LogType m_logType;
      private string m_logTimestampSourceTimeZoneId;
      private string m_logTimestampDestinationTimeZoneId;

      public enum MessageType { Info, Warning, Error, Mandatory };
      #endregion

      #region Methods
      /// <summary>
      /// The Logger 'Singleton' class.  It is used to write log messages to either the Event Log, a database table or both.
      /// The user can also choose the type of message to log, e.g., informational, warning or error.
      /// </summary>
      protected Logger()
      {
         // Set up event logging for this service
         m_eventLog = new EventLog();
         m_logType = GetLogType();

         // Get timezone information for the Timestamp of the log message
         m_logTimestampSourceTimeZoneId = ConfigurationManager.AppSettings["logTimestampSourceTimeZoneId"];
         m_logTimestampDestinationTimeZoneId = ConfigurationManager.AppSettings["logTimestampDestinationTimeZoneId"];

         string eventLogSource = ConfigurationManager.AppSettings["eventLogSource"];
         string eventLogName = ConfigurationManager.AppSettings["eventLogName"];

         //if (!EventLog.SourceExists(eventLogSource))
         //{
         //   EventLog.CreateEventSource(eventLogSource, eventLogName);
         //}

         m_eventLog.Source = eventLogSource;
         m_eventLog.Log = eventLogName;
      }

      /// <summary>
      /// The public method to get an instance of the Logger object.  This is also a thread-safe method
      /// </summary>
      /// <returns>The one and only logger instance</returns>
      public static Logger GetLogger()
      {
         // Support multithreaded applications through
         // 'Double checked locking' pattern which (once
         // the instance exists) avoids locking each
         // time the method is invoked            
         if (m_instance == null)
         {
            lock (m_syncLock)
            {
               if (m_instance == null)
               {
                  m_instance = new Logger();
               }
            }
         }
         return m_instance;
      }

      /// <summary>
      /// This method is what the application can call to log a message
      /// </summary>
      /// <param name="sentryTask">The task trying to write a log entry</param>
      /// <param name="message">The text of the message itself</param>
      /// <param name="messageType">The message type from the Message Type enumeration</param>
      public void WriteEntry(FruitCalculator.Program.TJCApplications application, string message, MessageType messageType)
      {
         try
         {
            // Bail if verbosity is turned off in the app.config file and the message type is INFO
            bool verbose = GetCustomApplicationLoggerVerbositySetting();
            if ((verbose == false) && (messageType == MessageType.Info))
            {
               return;
            }

            switch (m_logType)
            {
               case LogType.Console:
                  WriteConsoleLogEntry(application, message, messageType);
                  break;
               //case LogType.Database:
               //   WriteDatabaseLogEntry(application, message, messageType);
               //   break;
               case LogType.EventViewer:
                  WriteEventLogEntry(application, message, messageType);
                  break;
               case LogType.All:
                  WriteEventLogEntry(application, message, messageType);
                  WriteConsoleLogEntry(application, message, messageType);
                  break;
               default:
                  WriteConsoleLogEntry(application, message, messageType);
                  break;
            }
         }
         catch (Exception)
         {
            // Eat the error.  No need to core the app for logging
         }
      }

      /// <summary>
      /// This method handles writing messages to the system EventLog
      /// </summary>
      /// <param name="sentryTask">The task trying to write an event log entry</param>
      /// <param name="message">The text of the message itself</param>
      /// <param name="messageType">The message type from the Message Type enumeration</param>
      private void WriteEventLogEntry(FruitCalculator.Program.TJCApplications application, string message, MessageType messageType)
      {
         EventLogEntryType elMessageType = GetMessageTypeForEventLog(messageType);

         string eventLogMessage = application.ToString() + ":  " + message;
         try
         {
            m_eventLog.WriteEntry(eventLogMessage, elMessageType, m_eventId++);
         }
         catch (Exception)
         {
            // Eat the error.  No need to core the app for logging
         }
      }

      /// This method handles writing messages to the system EventLog
      /// </summary>
      /// <param name="sentryTask">The task trying to write an event log entry</param>
      /// <param name="message">The text of the message itself</param>
      /// <param name="messageType">The message type from the Message Type enumeration</param>
      private void WriteConsoleLogEntry(FruitCalculator.Program.TJCApplications application, string message, MessageType messageType)
      {
         EventLogEntryType elMessageType = GetMessageTypeForEventLog(messageType);

         string eventLogMessage = application.ToString() + ":  " + message;
         try
         {
            Console.WriteLine(application + ": " + messageType + ": " + message + ".");
         }
         catch (Exception)
         {
            // Eat the error.  No need to core the app for logging
            Console.WriteLine("An error occurred in writing the error to the console.  The error was:  " + message + "  Error type was:  " + messageType);
         }
      }

      /// <summary>
      /// This method handles writing messages to a table in a database
      /// </summary>
      /// <param name="sentryTask">The task writing the database log entry</param>
      /// <param name="message">The text of the message itself</param>
      /// <param name="messageType">The message type from the Message Type enumeration</param>
      /*
      private void WriteDatabaseLogEntry(FruitCalculator.Program.TJCApplications application, string message, MessageType messageType)
      {
         SqlConnection conn = null;
         string dbMessageType = GetMessageTypeForDatabase(messageType);

         try
         {
            if (Environment.GetInstance.CurrentEnvironment == SentryTasksService.EnvironmentOptions.PRODUCTION)
            {
               conn = SentryTasksService.GetCentralPortalProdDatabaseConnection();
            }
            else
            {
               conn = SentryTasksService.GetCentralPortalDevDatabaseConnection();
            }

            using (SqlCommand cmd = new SqlCommand("sp_SentryTasksService_Audit", conn))
            {
               cmd.CommandType = CommandType.StoredProcedure;

               cmd.Parameters.Add("@p_SentryTask", SqlDbType.VarChar).Value = application.ToString();
               cmd.Parameters.Add("@p_LogMessage", SqlDbType.VarChar).Value = message;
               cmd.Parameters.Add("@p_MessageType", SqlDbType.VarChar).Value = GetMessageTypeForDatabase(messageType);
               cmd.Parameters.Add("@p_Timestamp", SqlDbType.DateTime2).Value = DateTime.Now;
               //cmd.Parameters.Add("@p_Timestamp", SqlDbType.DateTime2).Value = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.Now, _logTimestampSourceTimeZoneId, _logTimestampDestinationTimeZoneId);

               conn.Open();
               cmd.ExecuteNonQuery();
               conn.Close();
            }
         }
         catch (Exception ex)
         {
            // Eat the error.  No need to core the app for logging
            WriteEventLogEntry(application, "Error occurred writing to database.  Error was:  " + ex.Message, MessageType.Error);
            if (conn != null && conn.State == System.Data.ConnectionState.Open)
            {
               conn.Close();
            }
         }
      }
      */
      /// <summary>
      /// This method gets the proper Event Log Entry type given the message type that is passed in.
      /// </summary>
      /// <param name="messageType">Message type from the MessageType enum defined in this class</param>
      /// <returns></returns>
      private EventLogEntryType GetMessageTypeForEventLog(MessageType messageType)
      {
         EventLogEntryType elMessageType;
         try
         {
            switch (messageType)
            {
               // These are messages that are important and have an Information Type
               case MessageType.Mandatory:
                  elMessageType = EventLogEntryType.Information;
                  break;
               case MessageType.Info:
                  elMessageType = EventLogEntryType.Information;
                  break;
               case MessageType.Warning:
                  elMessageType = EventLogEntryType.Warning;
                  break;
               case MessageType.Error:
                  elMessageType = EventLogEntryType.Error;
                  break;
               default:
                  elMessageType = EventLogEntryType.Information;
                  break;
            }
         }
         catch (Exception)
         {
            elMessageType = EventLogEntryType.Error;
            // Eat the error.  No need to core the application for logging.
         }

         return elMessageType;
      }

      /// <summary>
      /// This method gets the proper database message type given the message type that is passed in.
      /// </summary>
      /// <param name="messageType">Message type from the MessageType enum defined in this class</param>
      /// <returns></returns>
      private string GetMessageTypeForDatabase(MessageType messageType)
      {
         string dbMessageType;
         try
         {
            switch (messageType)
            {
               case MessageType.Mandatory:
                  dbMessageType = "Information";
                  break;
               case MessageType.Info:
                  dbMessageType = "Information";
                  break;
               case MessageType.Warning:
                  dbMessageType = "Warning";
                  break;
               case MessageType.Error:
                  dbMessageType = "Error";
                  break;
               default:
                  dbMessageType = "Default";
                  break;
            }
         }
         catch (Exception)
         {
            dbMessageType = "error setting message type";
            // Eat the error.  No need to core the application for logging.
         }

         return dbMessageType;
      }

      /*
      /// <summary>
      /// This method gets the proper database message type given the message type that is passed in.
      /// </summary>
      /// <param name="messageType">Message type from the MessageType enum defined in this class</param>
      /// <returns></returns>
      private string GetSentryTaskForLogging(SentryTask sentryTask)
      {
          string logSentryTask;
          try
          {
              switch (sentryTask)
              {
                  case SentryTask.SentryTaskService:
                      logSentryTask = "SentryTasksService";
                      break;
                  case SentryTask.AlertCleanup:
                      logSentryTask = "AlertCleanup";
                      break;
                  case SentryTask.ExampleJob:
                      logSentryTask = "ExampleJob";
                      break;
                  case SentryTask.ToddJob:
                      logSentryTask = "ToddJob";
                      break;
                  case SentryTask.LabsoftSendCoc:
                      logSentryTask = "LabsoftSendCoc";
                      break;
                  case SentryTask.ExpiredPrescriptionNotice:
                      logSentryTask = "ExpiredPrescriptionNotice";
                      break;
                  case SentryTask.HorizonImportSentryTable:
                      logSentryTask = "HorizonImportSentryTable";
                      break;
                  default:
                      logSentryTask = "Default Sentry Task";
                      break;
              }
          }
          catch (Exception)
          {
              logSentryTask = "error setting message type";
              // Eat the error.  No need to core the application for logging.
          }

          return logSentryTask;
      }
      */

      /// <summary>
      /// Gets the type of logging based on the setting in the app.config file
      /// </summary>
      /// <returns></returns>
      private LogType GetLogType()
      {
         LogType logType;

         int settingsLogType = Convert.ToInt32(ConfigurationManager.AppSettings["logType"]);

         switch (settingsLogType)
         {
            case 0: // Console
               logType = LogType.Console;
               break;
            case 1: // Console
               logType = LogType.Database;
               break;
            case 2: // EventViewer
               logType = LogType.EventViewer;
               break;
            case 3: // All
               logType = LogType.All;
               break;
            default:
               logType = LogType.Console;
               break;
         }
         return logType;
      }

      private bool GetCustomApplicationLoggerVerbositySetting()
      {
         bool verbosity = true;
         ConfigurationManager.RefreshSection("customAppSettingsGroup/customAppSettings");
         NameValueCollection customSettings = ConfigurationManager.GetSection("customAppSettingsGroup/customAppSettings") as NameValueCollection;
         if (customSettings != null)
         {
            verbosity = bool.Parse(customSettings.Get("loggerVerbosity"));
         }
         return verbosity;
      }
      #endregion
   }
}
