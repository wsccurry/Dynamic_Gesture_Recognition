import pandas as pd
file=open('./OriginData.txt')
lines=file.readlines()
rows=len(lines)
to_DataFrame_rows=[]
for line in lines:
    dp={'0_Proportion':0,'1_Proportion':0,'2_Proportion':0,'3_Proportion':0,'4_Proportion':0,'5_Proportion':0,'6_Proportion':0,'7_Proportion':0}
    row=line.strip().split(' ')
    lens=len(row)
    for i in range(1,lens):
        if row[i]=='0':
            dp['0_Proportion']=dp['0_Proportion']+1
        elif row[i]=='1':
            dp['1_Proportion']=dp['1_Proportion']+1
        elif row[i]=='2':
            dp['2_Proportion']=dp['2_Proportion']+1
        elif row[i]=='3':
            dp['3_Proportion']=dp['3_Proportion']+1
        elif row[i]=='4':
            dp['4_Proportion']=dp['4_Proportion']+1
        elif row[i]=='5':
            dp['5_Proportion']=dp['5_Proportion']+1
        elif row[i]=='6':
            dp['6_Proportion']=dp['6_Proportion']+1
        else:
            dp['7_Proportion']=dp['7_Proportion']+1
    for index in range(0,8):
        key=str(index)+'_Proportion'
        dp[key]=round(dp[key]/lens,3)
    dp['label']=int(row[0])
    to_DataFrame_rows.append(dp)
data=pd.DataFrame(to_DataFrame_rows)
data.to_csv('./data.csv')