cd ..
dotnet run -c Release -e ${ENDPOINT} -k ${KEY} -t ${THROUGHPUT} -n ${DOCUMENTS} --pl ${PARALLELISM} --CleanupOnFinish ${CLEANUPFINISH} -w InsertV2BenchmarkOperation