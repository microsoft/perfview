import React, { useEffect, useState } from "react";
import {
  DetailsList, DetailsListLayoutMode, IColumn, Selection, IStackProps,
  SelectionMode, Stack, Text, Link
} from "@fluentui/react";
import { Container, Row } from "react-grid-system";
import { useTranslation } from "react-i18next";
import { useDataFileContext } from '../context/DataFileContext';

const stackTokens = { childrenGap: 50 };
const columnProps: Partial<IStackProps> = {
  tokens: { childrenGap: 5 },
};

const columns: IColumn[] = [
  {
    key: 'column1',
    name: 'Trace files',
    fieldName: 'name',
    minWidth: 510,
    isRowHeader: true,
    data: 'string'
  }
];

const Home = () => {
  const { t } = useTranslation();
  const [files, setFiles] = useState<string[]>([]);
  const { setDataFile, } = useDataFileContext();

  useEffect(() => {
    fetch("/api/datadirectorylisting", {
      method: "GET",
      headers: { "Content-Type": "application/json" },
    })
      .then((res) => res.json())
      .then((data) => {
        setFiles(data);
      });
  }, []);

  const transformToDetailListItems = (items: string[]) => {
    return items.map((item, i) => {
      return {
        key: i,
        name: item,
        value: item
      }
    });
  }

  const selection = new Selection({
    onSelectionChanged: () => {
      if (selection.getSelection().length > 0) {
        //?workaround for Fluent-UI, since it's always an array
        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        //@ts-ignore
        const selectedFile = selection.getSelection()[0].value;
        setDataFile(selectedFile);
      }
    },
    selectionMode: SelectionMode.single,
  });
  return (
    <Container>
      <Row>
        <Stack>
          <Text variant={'xLarge'} block>{t('home.title')}</Text>
          <Text variant={'large'}>
            {t('home.intro1')}{' '}
            <Link target="_blank" href={"https://github.com/microsoft/perfview/tree/main/src/PerfViewJS"} underline>{t('home.githubtext')}</Link>
          </Text>
          <Text>
            {t('home.intro2')}
            <Link target="_blank" href={"https://github.com/dotnet/diagnostics/blob/main/documentation/dotnet-trace-instructions.md"} underline>
              {t('home.tracedocs')}
            </Link>
          </Text>
          <Text variant={'large'}>
            {t('home.bugreporting')}{' '}
            <Link target="_blank" href={"https://github.com/microsoft/perfview/issues"} underline>{t('home.bugreportingtext')}</Link>
          </Text>
        </Stack>
      </Row>
      <Row>
        <Stack horizontal tokens={stackTokens} >
          <Stack {...columnProps}>
            {/* <TextField label={'Input file'} value={dataFileName} readOnly /> */}
            <DetailsList
              items={files ? transformToDetailListItems(files) : []}
              columns={columns}
              selection={selection}
              selectionMode={SelectionMode.single}
              layoutMode={DetailsListLayoutMode.justified}
            />
          </Stack>
        </Stack>
      </Row>
    </Container>
  );
}
export default Home;
