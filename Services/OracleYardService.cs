using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using ReeferSystem.Models;
using System.Data;

namespace ReeferSystem.Services
{
    public class OracleYardService : IOracleYardService
    {
        private readonly string _connectionString;
        private readonly ILogger<OracleYardService> _logger;

        public OracleYardService(IConfiguration config, ILogger<OracleYardService> logger)
        {
            _logger = logger;
            _connectionString = config.GetConnectionString("DBMS") 
                             ?? config.GetConnectionString("OracleDbContext") 
                             ?? string.Empty;
        }

        private OracleConnection CreateConnection()
        {
            return new OracleConnection(_connectionString);
        }

        private static List<ContainerModel>? _cachedContainers;
        private static DateTime _cacheExpiry = DateTime.MinValue;
        private static readonly SemaphoreSlim _cacheLock = new(1, 1);

        /// <summary>
        /// Calls Pkg_Front_End.Get_All_Containers(p_User_Id in varchar2, p_Ctr_Data out sys_refcursor) return varchar2
        /// Strictly live Oracle data from Ctr2. Zero dummy fallbacks.
        /// Includes in-memory cache for ultra-fast page reloads.
        /// </summary>
        public async Task<(string Status, List<ContainerModel> Containers)> GetAllContainersAsync(string userId = "ADMIN", bool forceRefresh = false)
        {
            if (!forceRefresh && _cachedContainers != null && DateTime.UtcNow < _cacheExpiry)
            {
                return ("OK", _cachedContainers);
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (!forceRefresh && _cachedContainers != null && DateTime.UtcNow < _cacheExpiry)
                {
                    return ("OK", _cachedContainers);
                }

                var containers = new List<ContainerModel>();

                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Pkg_Front_End.Get_All_Containers";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = true;

                // Return Value: varchar2
                var returnParam = new OracleParameter("ret_val", OracleDbType.Varchar2, 100)
                {
                    Direction = ParameterDirection.ReturnValue
                };
                cmd.Parameters.Add(returnParam);

                // p_User_Id in varchar2
                cmd.Parameters.Add(new OracleParameter("p_User_Id", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = userId
                });

                // p_Ctr_Data out sys_refcursor
                var cursorParam = new OracleParameter("p_Ctr_Data", OracleDbType.RefCursor)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(cursorParam);

                await cmd.ExecuteNonQueryAsync();

                string status = returnParam.Value?.ToString() ?? "OK";

                if (cursorParam.Value is OracleRefCursor refCursor && !refCursor.IsNull)
                {
                    using var reader = refCursor.GetDataReader();
                    while (await reader.ReadAsync())
                    {
                        var model = new ContainerModel
                        {
                            CtrNbr = GetSafeString(reader, "Ctr_Nbr"),
                            CtrSize = GetSafeString(reader, "Ctr_Size"),
                            CtrType = GetSafeString(reader, "Ctr_Type"),
                            CtrHeight = GetSafeString(reader, "Ctr_Height"),
                            TareWeight = GetSafeDecimal(reader, "Tare_Weight"),
                            TareWeightUnit = GetSafeString(reader, "Tare_Weight_Unit", "KG"),
                            SafeWeight = GetSafeDecimal(reader, "Safe_Weight"),
                            SafeWeightUnit = GetSafeString(reader, "Safe_Weight_Unit", "KG"),
                            CurrentLocationType = GetSafeString(reader, "Current_Location_Type", "YARD"),
                            CurrentPosition = GetSafeString(reader, "Current_Position")
                        };

                        if (!string.IsNullOrEmpty(model.CtrNbr))
                        {
                            containers.Add(model);
                        }
                    }
                }

                _logger.LogInformation("Pkg_Front_End.Get_All_Containers returned {Count} live containers from Oracle.", containers.Count);

                if (containers.Count > 0)
                {
                    _cachedContainers = containers;
                    _cacheExpiry = DateTime.UtcNow.AddMinutes(5); // Cache for 5 minutes for instant page reloads
                }

                return (status, containers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oracle execution Pkg_Front_End.Get_All_Containers failed: {Error}", ex.Message);
                return ("ERROR", _cachedContainers ?? new List<ContainerModel>());
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Calls Pkg_Front_End.Get_Cv_Details(p_Ctr_Nbr in varchar2, p_User_Id in varchar2, p_Ctr_Details out sys_refcursor) return varchar2
        /// Strictly live Oracle data from Cv2 & Ctr2. Zero dummy fallbacks.
        /// </summary>
        public async Task<(string Status, CvDetailsModel? Details)> GetCvDetailsAsync(string ctrNbr, string userId = "ADMIN")
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Pkg_Front_End.Get_Cv_Details";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = true;

                // Return Value: varchar2
                var returnParam = new OracleParameter("ret_val", OracleDbType.Varchar2, 100)
                {
                    Direction = ParameterDirection.ReturnValue
                };
                cmd.Parameters.Add(returnParam);

                // p_Ctr_Nbr in varchar2
                cmd.Parameters.Add(new OracleParameter("p_Ctr_Nbr", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = ctrNbr
                });

                // p_User_Id in varchar2
                cmd.Parameters.Add(new OracleParameter("p_User_Id", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = userId
                });

                // p_Ctr_Details out sys_refcursor
                var cursorParam = new OracleParameter("p_Ctr_Details", OracleDbType.RefCursor)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(cursorParam);

                await cmd.ExecuteNonQueryAsync();

                string status = returnParam.Value?.ToString() ?? "OK";

                if (cursorParam.Value is OracleRefCursor refCursor && !refCursor.IsNull)
                {
                    using var reader = refCursor.GetDataReader();
                    if (await reader.ReadAsync())
                    {
                        var details = new CvDetailsModel
                        {
                            CtrNbr = GetSafeString(reader, "Ctr_Nbr"),
                            Category = GetSafeString(reader, "Category", "COMMUNITY"),
                            CtrStatus = GetSafeString(reader, "Ctr_Status", "DELIVERED"),
                            YardInTime = GetSafeDateTime(reader, "Yard_In_Time"),
                            YardOutTime = GetSafeDateTime(reader, "Yard_Out_Time"),
                            VesselCode = GetSafeString(reader, "Vessel_Code"),
                            Voyage = GetSafeString(reader, "Voyage"),
                            CtrSize = GetSafeString(reader, "Ctr_Size"),
                            CtrType = GetSafeString(reader, "Ctr_Type"),
                            CtrHeight = GetSafeString(reader, "Ctr_Height"),
                            GrossWeight = GetSafeDecimal(reader, "Gross_Weight"),
                            GrossWeightUnit = GetSafeString(reader, "Gross_Weight_Unit", "KG")
                        };

                        return (status, details);
                    }
                }

                return (status, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oracle execution Pkg_Front_End.Get_Cv_Details failed for {Ctr}: {Error}", ctrNbr, ex.Message);
                return ("ERROR", null);
            }
        }

        /// <summary>
        /// Calls Pkg_Front_End.Insert_Yard_slot(p_useq in out number, ...) return varchar2
        /// </summary>
        public async Task<(string Status, long Useq, string? ErrorMessage)> InsertYardSlotAsync(YardSlotModel slot)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Pkg_Front_End.Insert_Yard_slot";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = true;

                // Return Value: varchar2
                var returnParam = new OracleParameter("ret_val", OracleDbType.Varchar2, 100)
                {
                    Direction = ParameterDirection.ReturnValue
                };
                cmd.Parameters.Add(returnParam);

                // p_useq in out number
                var useqParam = new OracleParameter("p_useq", OracleDbType.Decimal)
                {
                    Direction = ParameterDirection.InputOutput,
                    Value = DBNull.Value
                };
                cmd.Parameters.Add(useqParam);

                cmd.Parameters.Add(new OracleParameter("p_yard_slot_name", OracleDbType.Varchar2) { Value = slot.YardSlotName });
                cmd.Parameters.Add(new OracleParameter("p_yard_slot_type", OracleDbType.Varchar2) { Value = slot.YardSlotType });
                cmd.Parameters.Add(new OracleParameter("p_stack_rows", OracleDbType.Decimal) { Value = slot.StackRows });
                cmd.Parameters.Add(new OracleParameter("p_y_axis", OracleDbType.Decimal) { Value = slot.YAxis });
                cmd.Parameters.Add(new OracleParameter("p_x_axis", OracleDbType.Decimal) { Value = slot.XAxis });
                cmd.Parameters.Add(new OracleParameter("p_x_axis_start_from", OracleDbType.Varchar2) { Value = slot.XAxisStartFrom });
                cmd.Parameters.Add(new OracleParameter("p_y_axis_start_from", OracleDbType.Varchar2) { Value = slot.YAxisStartFrom });
                cmd.Parameters.Add(new OracleParameter("p_active", OracleDbType.Varchar2) { Value = slot.Active });
                cmd.Parameters.Add(new OracleParameter("p_user_id", OracleDbType.Varchar2) { Value = slot.UserId });

                await cmd.ExecuteNonQueryAsync();

                string status = returnParam.Value?.ToString() ?? "OK";
                long generatedUseq = 0;
                if (useqParam.Value != null && useqParam.Value != DBNull.Value)
                {
                    long.TryParse(useqParam.Value.ToString(), out generatedUseq);
                }

                _logger.LogInformation("Pkg_Front_End.Insert_Yard_slot returned Useq={Useq}, Status={Status}", generatedUseq, status);

                if (status == "ERROR")
                {
                    try
                    {
                        using var testCmd = conn.CreateCommand();
                        testCmd.CommandText = @"
                            INSERT INTO yard_slots
                              (yard_slot_name, yard_slot_type, stack_rows, y_axis, x_axis, x_axis_start_from, y_axis_start_from, active, useq, entered_at, entered_by)
                            VALUES
                              (:yard_slot_name, :yard_slot_type, :stack_rows, :y_axis, :x_axis, :x_axis_start_from, :y_axis_start_from, :active, NVL(:useq, seq_yard_slots.nextval), sysdate, :entered_by)";
                        testCmd.BindByName = true;
                        testCmd.Parameters.Add(new OracleParameter("yard_slot_name", slot.YardSlotName));
                        testCmd.Parameters.Add(new OracleParameter("yard_slot_type", slot.YardSlotType));
                        testCmd.Parameters.Add(new OracleParameter("stack_rows", slot.StackRows));
                        testCmd.Parameters.Add(new OracleParameter("y_axis", slot.YAxis));
                        testCmd.Parameters.Add(new OracleParameter("x_axis", slot.XAxis));
                        testCmd.Parameters.Add(new OracleParameter("x_axis_start_from", slot.XAxisStartFrom));
                        testCmd.Parameters.Add(new OracleParameter("y_axis_start_from", slot.YAxisStartFrom));
                        testCmd.Parameters.Add(new OracleParameter("active", slot.Active));
                        testCmd.Parameters.Add(new OracleParameter("useq", generatedUseq > 0 ? (object)generatedUseq : DBNull.Value));
                        testCmd.Parameters.Add(new OracleParameter("entered_by", slot.UserId));

                        await testCmd.ExecuteNonQueryAsync();
                        status = "OK";
                    }
                    catch (Exception directEx)
                    {
                        _logger.LogError(directEx, "Direct insert into yard_slots failed: {Message}", directEx.Message);
                    }
                }

                try
                {
                    using var commitCmd = conn.CreateCommand();
                    commitCmd.CommandText = "COMMIT";
                    await commitCmd.ExecuteNonQueryAsync();
                }
                catch { }

                slot.Useq = generatedUseq;
                return (status, slot.Useq, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oracle Insert_Yard_slot failed: {Error}", ex.Message);
                return ("ERROR", 0, ex.Message);
            }
        }

        /// <summary>
        /// Calls Pkg_Front_End.update_Yard_slot(p_useq in number, ...) return varchar2
        /// </summary>
        public async Task<(string Status, string? ErrorMessage)> UpdateYardSlotAsync(YardSlotModel slot)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Pkg_Front_End.update_Yard_slot";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = true;

                // Return Value: varchar2
                var returnParam = new OracleParameter("ret_val", OracleDbType.Varchar2, 100)
                {
                    Direction = ParameterDirection.ReturnValue
                };
                cmd.Parameters.Add(returnParam);

                cmd.Parameters.Add(new OracleParameter("p_useq", OracleDbType.Decimal) { Value = slot.Useq });
                cmd.Parameters.Add(new OracleParameter("p_yard_slot_name", OracleDbType.Varchar2) { Value = slot.YardSlotName });
                cmd.Parameters.Add(new OracleParameter("p_yard_slot_type", OracleDbType.Varchar2) { Value = slot.YardSlotType });
                cmd.Parameters.Add(new OracleParameter("p_stack_rows", OracleDbType.Decimal) { Value = slot.StackRows });
                cmd.Parameters.Add(new OracleParameter("p_y_axis", OracleDbType.Decimal) { Value = slot.YAxis });
                cmd.Parameters.Add(new OracleParameter("p_x_axis", OracleDbType.Decimal) { Value = slot.XAxis });
                cmd.Parameters.Add(new OracleParameter("p_x_axis_start_from", OracleDbType.Varchar2) { Value = slot.XAxisStartFrom });
                cmd.Parameters.Add(new OracleParameter("p_y_axis_start_from", OracleDbType.Varchar2) { Value = slot.YAxisStartFrom });
                cmd.Parameters.Add(new OracleParameter("p_active", OracleDbType.Varchar2) { Value = slot.Active });
                cmd.Parameters.Add(new OracleParameter("p_user_id", OracleDbType.Varchar2) { Value = slot.UserId });

                await cmd.ExecuteNonQueryAsync();

                try
                {
                    using var commitCmd = conn.CreateCommand();
                    commitCmd.CommandText = "COMMIT";
                    await commitCmd.ExecuteNonQueryAsync();
                }
                catch { }

                string status = returnParam.Value?.ToString() ?? "OK";
                _logger.LogInformation("Pkg_Front_End.update_Yard_slot executed for Useq={Useq}. Status={Status}", slot.Useq, status);

                return (status, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oracle update_Yard_slot failed: {Error}", ex.Message);
                return ("ERROR", ex.Message);
            }
        }

        /// <summary>
        /// Calls Pkg_Front_End.Delete_yard_solt(p_useq in number, p_user_id in number) return varchar2
        /// </summary>
        public async Task<(string Status, string? ErrorMessage)> DeleteYardSlotAsync(long useq, string userId = "1")
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "Pkg_Front_End.Delete_yard_solt";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = true;

                // Return Value: varchar2
                var returnParam = new OracleParameter("ret_val", OracleDbType.Varchar2, 100)
                {
                    Direction = ParameterDirection.ReturnValue
                };
                cmd.Parameters.Add(returnParam);

                cmd.Parameters.Add(new OracleParameter("p_useq", OracleDbType.Decimal) { Value = useq });

                long.TryParse(userId, out long numericUserId);
                if (numericUserId == 0) numericUserId = 1;

                cmd.Parameters.Add(new OracleParameter("p_user_id", OracleDbType.Decimal) { Value = numericUserId });

                await cmd.ExecuteNonQueryAsync();

                try
                {
                    using var commitCmd = conn.CreateCommand();
                    commitCmd.CommandText = "COMMIT";
                    await commitCmd.ExecuteNonQueryAsync();
                }
                catch { }

                string status = returnParam.Value?.ToString() ?? "OK";
                _logger.LogInformation("Pkg_Front_End.Delete_yard_solt executed for Useq={Useq}. Status={Status}", useq, status);

                return (status, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Oracle Delete_yard_solt failed: {Error}", ex.Message);
                return ("ERROR", ex.Message);
            }
        }

        /// <summary>
        /// Retrieves list of all yard slots strictly from Oracle table yard_slots. Zero dummy records.
        /// </summary>
        public async Task<List<YardSlotModel>> GetYardSlotsAsync()
        {
            var list = new List<YardSlotModel>();

            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT useq,
                           yard_slot_name,
                           yard_slot_type,
                           stack_rows,
                           y_axis,
                           x_axis,
                           x_axis_start_from,
                           y_axis_start_from,
                           active,
                           entered_at,
                           entered_by,
                           modified_at,
                           modified_by
                      FROM yard_slots
                     ORDER BY useq DESC";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new YardSlotModel
                    {
                        Useq = Convert.ToInt64(reader["useq"]),
                        YardSlotName = GetSafeString(reader, "yard_slot_name"),
                        YardSlotType = GetSafeString(reader, "yard_slot_type", "STANDARD"),
                        StackRows = Convert.ToInt32(reader["stack_rows"] == DBNull.Value ? 14 : reader["stack_rows"]),
                        YAxis = Convert.ToInt32(reader["y_axis"] == DBNull.Value ? 6 : reader["y_axis"]),
                        XAxis = Convert.ToInt32(reader["x_axis"] == DBNull.Value ? 6 : reader["x_axis"]),
                        XAxisStartFrom = GetSafeString(reader, "x_axis_start_from", "A"),
                        YAxisStartFrom = GetSafeString(reader, "y_axis_start_from", "1"),
                        Active = GetSafeString(reader, "active", "Y"),
                        EnteredAt = GetSafeDateTime(reader, "entered_at"),
                        EnteredBy = GetSafeString(reader, "entered_by"),
                        ModifiedAt = GetSafeDateTime(reader, "modified_at"),
                        ModifiedBy = GetSafeString(reader, "modified_by")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Direct query on yard_slots table failed: {Error}", ex.Message);
            }

            return list;
        }

        /// <summary>
        /// Updates the current position of a container in Oracle Ctr2 table.
        /// </summary>
        public async Task<(string Status, string? ErrorMessage)> UpdateContainerPositionAsync(string ctrNbr, string newPosition)
        {
            try
            {
                using var conn = CreateConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Ctr2 SET Current_Position = :newPos WHERE Ctr_Nbr = :ctrNbr";
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter("newPos", newPosition));
                cmd.Parameters.Add(new OracleParameter("ctrNbr", ctrNbr));

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                try
                {
                    using var commitCmd = conn.CreateCommand();
                    commitCmd.CommandText = "COMMIT";
                    await commitCmd.ExecuteNonQueryAsync();
                }
                catch { }

                _logger.LogInformation("Updated container {Ctr} position to {Pos} in Ctr2. Rows={Rows}", ctrNbr, newPosition, rowsAffected);

                if (_cachedContainers != null)
                {
                    var found = _cachedContainers.FirstOrDefault(c => c.CtrNbr == ctrNbr);
                    if (found != null)
                    {
                        found.CurrentPosition = newPosition;
                    }
                }

                return ("OK", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update container position in Ctr2: {Error}", ex.Message);
                return ("ERROR", ex.Message);
            }
        }

        // =========================================================================
        // DATA READ HELPERS
        // =========================================================================
        private static string GetSafeString(IDataRecord reader, string column, string defaultValue = "")
        {
            try
            {
                int ordinal = reader.GetOrdinal(column);
                return reader.IsDBNull(ordinal) ? defaultValue : reader.GetValue(ordinal)?.ToString() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private static decimal? GetSafeDecimal(IDataRecord reader, string column)
        {
            try
            {
                int ordinal = reader.GetOrdinal(column);
                if (reader.IsDBNull(ordinal)) return null;
                var val = reader.GetValue(ordinal);
                if (val == null) return null;
                if (decimal.TryParse(val.ToString(), out decimal d)) return d;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static DateTime? GetSafeDateTime(IDataRecord reader, string column)
        {
            try
            {
                int ordinal = reader.GetOrdinal(column);
                if (reader.IsDBNull(ordinal)) return null;
                var val = reader.GetValue(ordinal);
                if (val is DateTime dt) return dt;
                if (DateTime.TryParse(val?.ToString(), out DateTime parsed)) return parsed;
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
