# Tfs2Git
tools to assist migrating from tfs repo to git repo
The tools in this project are a collection of the code I wrote to migrate our TFS projects on TFS-server to GIT projects on TFS-server. It allows you to migrate workitems (with history!), testplans, workitem queries to a new TFS project. You can then use git-tfs to migrate the code and restore the link between the code commits and your work items.
The code was written so that it allows to migrate from one process template to another.

The code is provided as is, I will not be held responsible if it eats kittens or destroys your tfs server. Btw, I know it's ugly code.

## Project to migrate workitems with history from one tfs project to another

### BuildHistoryInDatabase
Runs through the history of all workitems in tfsproject and stores revisions in database. Just references for future use and dependency sorting and tracking of what was migrated...
	
### GenerateFieldMappings
Creates mapping between fields that are available on source tfs project and target tfs project, allows discarding of fields no longer in use. Output is an xml file you are supposed to have a look at. If you see that fields or field values were not uniquely mapped to new fields or field values (find '|' character in the file) on the new project, edit this file.

### CreateWorkItems
Does the actual migration, keeps progress in database created in 'BuildHistoryInDatabase'. Also migrates Areas, Iterations, Testplans if specified.

### CreateMappingFile
Creates a mappingfile between workitem of old tfs project and new tfs project to be used with git-tfs

### CreateAUTHORSfile
Creates an AUTHORS file that git-tfs can use to transform the tfs author to a git author based on the AD with directory searcher.

### Data
Database model
	
### Utils
More code.	

## Typical workflow 
	1. GenerateFieldMappings.exe -i "https://tfsserver:8080/tfs/DefaultCollection" -p "SourceProject" -n "https://tfsserver:8080/tfs/newDefaultCollection" -t "TargetProject" -o "c:\git\migration_TargetProject.xml" -m "User Story:Product Backlog Item;Issue:Bug"
	2. Ammend the output file so all field mappings and field value mappings you want are in the xml file.
	3. BuildHistoryInDatabase.exe -i "https://tfsserver:8080/tfs/DefaultCollection" -p "SourceProject"
	4. CreateWorkItems.exe -i "https://tfsserver:8080/tfs/DefaultCollection" -p "SourceProject" -n "https://tfsserver:8080/tfs/newDefaultCollection" -t "TargetProject" -w "c:\git\migration_TargetProject.xml" -l c:\git\migration.log -a -f -q -e
	5. CreateMappingFile.exe -o "c:\git\mapping.txt"
	6. CreateAUTHORSfile.exe -i "http://tfsserver:8080/tfs/DefaultCollection" -p "SourceProject" -o "c:\git\AUTHORS.txt" -d "tfs@home.org"
	7. Use git-tfs to migrate the code (see below)


## Git-tfs stuff
	1.	[CMD] > git tfs clone http://tfsserver:8080/tfs/DefaultCollection "$/SourceProject/Main" --with-branches --authors=c:\git\AUTHORS.txt --export --export-work-item-mapping=c:\git\mapping.txt
	2.	git remote add origin http://tfsserver:8080/newDefaultCollection/_git/TargetProject
	3.	git push origin --all
